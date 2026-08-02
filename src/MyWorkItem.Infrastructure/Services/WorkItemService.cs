using System.Data.Common;
using Dapper;
using MyWorkItem.Application.Abstractions;
using MyWorkItem.Application.Contracts;
using MyWorkItem.Application.Exceptions;
using MyWorkItem.Domain.Constants;
using SqlKata;
using SqlKata.Compilers;

namespace MyWorkItem.Infrastructure.Services;

public sealed class WorkItemService(IDbConnectionFactory connectionFactory, IClock clock) : IWorkItemService
{
    private static readonly Guid ConfirmStatusId = Guid.Parse("33333333-3333-3333-3333-333333333332");
    private static readonly IReadOnlyDictionary<string, Guid> ActionIds = new Dictionary<string, Guid>
    {
        [ActionCodes.Insert] = Guid.Parse("44444444-4444-4444-4444-444444444441"),
        [ActionCodes.Update] = Guid.Parse("44444444-4444-4444-4444-444444444442"),
        [ActionCodes.Delete] = Guid.Parse("44444444-4444-4444-4444-444444444443")
    };

    public async Task<PagedResponse<WorkItemResponse>> QueryAsync(
        Guid userId,
        WorkItemQuery query,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var baseQuery = BuildBaseQuery(userId, query.Keyword, query.AssignedUserId);
        var compiler = new SqlServerCompiler();
        var countResult = compiler.Compile(baseQuery.Clone().AsCount());
        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            countResult.Sql, countResult.NamedBindings, cancellationToken: cancellationToken));

        var order = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
        var dataQuery = baseQuery
            .Select(
                "w.WorkItemId", "w.Title", "w.Description", "w.CreatedByUserId", "w.AssignedUserId",
                "w.CreatedAt", "w.UpdatedAt", "w.RowVersion", "s.ConfirmedAt")
            .SelectRaw("CASE WHEN s.UserId IS NULL THEN 'Pending' ELSE 'Confirm' END AS StatusCode")
            .When(order == "asc", q => q.OrderBy("w.CreatedAt"), q => q.OrderByDesc("w.CreatedAt"))
            .ForPage(query.Page, query.PageSize);
        var compiled = compiler.Compile(dataQuery);
        var rows = await connection.QueryAsync<WorkItemRow>(new CommandDefinition(
            compiled.Sql, compiled.NamedBindings, cancellationToken: cancellationToken));
        var items = rows.Select(Map).ToArray();
        return new PagedResponse<WorkItemResponse>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize));
    }

    public async Task<WorkItemResponse?> GetAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var row = await GetRowAsync(connection, null, userId, workItemId, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<WorkItemResponse> CreateAsync(
        Guid userId,
        CreateWorkItemRequest request,
        CancellationToken cancellationToken)
    {
        ValidateWorkItem(request.Title);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ValidateAssignedUserAsync(connection, transaction, request.AssignedUserId, cancellationToken);

        var now = clock.UtcNow;
        var workItemId = Guid.NewGuid();
        var row = await connection.QuerySingleAsync<WorkItemMutationRow>(new CommandDefinition(
            """
            INSERT dbo.WorkItems
                (WorkItemId, Title, Description, CreatedByUserId, AssignedUserId, CreatedAt, UpdatedAt)
            OUTPUT INSERTED.WorkItemId, INSERTED.Title, INSERTED.Description, INSERTED.CreatedByUserId,
                   INSERTED.AssignedUserId, INSERTED.CreatedAt, INSERTED.UpdatedAt, INSERTED.DeletedAt,
                   INSERTED.DeletedByUserId, INSERTED.RowVersion
            VALUES
                (@WorkItemId, @Title, @Description, @UserId, @AssignedUserId, @Now, @Now)
            """,
            new
            {
                WorkItemId = workItemId,
                Title = request.Title.Trim(),
                request.Description,
                UserId = userId,
                request.AssignedUserId,
                Now = now
            }, transaction, cancellationToken: cancellationToken));
        await InsertHistoryAsync(connection, transaction, row, ActionCodes.Insert, userId, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(row, WorkItemStatusCodes.Pending, null);
    }

    public async Task<WorkItemResponse> UpdateAsync(
        Guid userId,
        Guid workItemId,
        UpdateWorkItemRequest request,
        CancellationToken cancellationToken)
    {
        ValidateWorkItem(request.Title);
        byte[] rowVersion;
        try
        {
            rowVersion = Convert.FromBase64String(request.RowVersion);
        }
        catch (FormatException exception)
        {
            throw new RequestValidationException("RowVersion 必須是有效的 Base64 字串。") { Source = exception.Source };
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ValidateAssignedUserAsync(connection, transaction, request.AssignedUserId, cancellationToken);
        var now = clock.UtcNow;
        var row = await connection.QuerySingleOrDefaultAsync<WorkItemMutationRow>(new CommandDefinition(
            """
            UPDATE dbo.WorkItems
            SET Title = @Title, Description = @Description, AssignedUserId = @AssignedUserId, UpdatedAt = @Now
            OUTPUT INSERTED.WorkItemId, INSERTED.Title, INSERTED.Description, INSERTED.CreatedByUserId,
                   INSERTED.AssignedUserId, INSERTED.CreatedAt, INSERTED.UpdatedAt, INSERTED.DeletedAt,
                   INSERTED.DeletedByUserId, INSERTED.RowVersion
            WHERE WorkItemId = @WorkItemId AND DeletedAt IS NULL AND RowVersion = @RowVersion
            """,
            new
            {
                WorkItemId = workItemId,
                Title = request.Title.Trim(),
                request.Description,
                request.AssignedUserId,
                Now = now,
                RowVersion = rowVersion
            }, transaction, cancellationToken: cancellationToken));

        if (row is null)
        {
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.WorkItems WHERE WorkItemId = @WorkItemId AND DeletedAt IS NULL) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END",
                new { WorkItemId = workItemId }, transaction, cancellationToken: cancellationToken));
            await transaction.RollbackAsync(cancellationToken);
            if (!exists)
            {
                throw new NotFoundException("找不到 Work Item。");
            }

            throw new ConflictException("Work Item 已由其他使用者更新，請重新載入後再試。");
        }

        await InsertHistoryAsync(connection, transaction, row, ActionCodes.Update, userId, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var response = await GetAsync(userId, workItemId, cancellationToken);
        return response ?? throw new NotFoundException("找不到 Work Item。");
    }

    public async Task DeleteAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = clock.UtcNow;
        var row = await connection.QuerySingleOrDefaultAsync<WorkItemMutationRow>(new CommandDefinition(
            """
            UPDATE dbo.WorkItems
            SET DeletedAt = @Now, DeletedByUserId = @UserId, UpdatedAt = @Now
            OUTPUT INSERTED.WorkItemId, INSERTED.Title, INSERTED.Description, INSERTED.CreatedByUserId,
                   INSERTED.AssignedUserId, INSERTED.CreatedAt, INSERTED.UpdatedAt, INSERTED.DeletedAt,
                   INSERTED.DeletedByUserId, INSERTED.RowVersion
            WHERE WorkItemId = @WorkItemId AND DeletedAt IS NULL
            """,
            new { WorkItemId = workItemId, UserId = userId, Now = now }, transaction,
            cancellationToken: cancellationToken));
        if (row is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new NotFoundException("找不到 Work Item。");
        }

        await InsertHistoryAsync(connection, transaction, row, ActionCodes.Delete, userId, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task ConfirmAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken) =>
        SetConfirmedAsync(userId, [workItemId], cancellationToken);

    public async Task RevokeConfirmationAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var exists = await WorkItemExistsAsync(connection, null, workItemId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("找不到 Work Item。");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE dbo.UserWorkItemStates WHERE UserId = @UserId AND WorkItemId = @WorkItemId",
            new { UserId = userId, WorkItemId = workItemId }, cancellationToken: cancellationToken));
    }

    public Task ConfirmBatchAsync(
        Guid userId,
        IReadOnlyCollection<Guid> workItemIds,
        CancellationToken cancellationToken)
    {
        var normalized = workItemIds.Distinct().ToArray();
        if (normalized.Length is < 1 or > 100)
        {
            throw new RequestValidationException("批次確認必須包含 1 至 100 筆 Work Item。");
        }

        return SetConfirmedAsync(userId, normalized, cancellationToken);
    }

    private async Task SetConfirmedAsync(
        Guid userId,
        IReadOnlyCollection<Guid> workItemIds,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var ids = workItemIds.Distinct().ToArray();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.WorkItems WITH (UPDLOCK, HOLDLOCK) WHERE WorkItemId IN @Ids AND DeletedAt IS NULL",
            new { Ids = ids }, transaction, cancellationToken: cancellationToken));
        if (count != ids.Length)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new NotFoundException("批次中包含不存在或已刪除的 Work Item。");
        }

        var now = clock.UtcNow;
        foreach (var id in ids)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.UserWorkItemStates
                SET WorkItemStatusId = @StatusId, ConfirmedAt = @Now, UpdatedAt = @Now
                WHERE UserId = @UserId AND WorkItemId = @WorkItemId;
                IF @@ROWCOUNT = 0
                    INSERT dbo.UserWorkItemStates
                        (UserId, WorkItemId, WorkItemStatusId, ConfirmedAt, UpdatedAt)
                    VALUES
                        (@UserId, @WorkItemId, @StatusId, @Now, @Now);
                """,
                new { UserId = userId, WorkItemId = id, StatusId = ConfirmStatusId, Now = now }, transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static Query BuildBaseQuery(Guid userId, string? keyword, Guid? assignedUserId)
    {
        var query = new Query("WorkItems as w")
            .LeftJoin("UserWorkItemStates as s", join => join
                .On("s.WorkItemId", "w.WorkItemId")
                .Where("s.UserId", userId))
            .WhereNull("w.DeletedAt");
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query.Where(inner => inner
                .WhereLike("w.Title", $"%{keyword.Trim()}%")
                .OrWhereLike("w.Description", $"%{keyword.Trim()}%"));
        }

        if (assignedUserId is not null)
        {
            query.Where("w.AssignedUserId", assignedUserId.Value);
        }

        return query;
    }

    private static async Task<WorkItemRow?> GetRowAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid userId,
        Guid workItemId,
        CancellationToken cancellationToken) => await connection.QuerySingleOrDefaultAsync<WorkItemRow>(new CommandDefinition(
            """
            SELECT w.WorkItemId, w.Title, w.Description, w.CreatedByUserId, w.AssignedUserId,
                   w.CreatedAt, w.UpdatedAt, w.RowVersion, s.ConfirmedAt,
                   CASE WHEN s.UserId IS NULL THEN 'Pending' ELSE 'Confirm' END AS StatusCode
            FROM dbo.WorkItems w
            LEFT JOIN dbo.UserWorkItemStates s ON s.WorkItemId = w.WorkItemId AND s.UserId = @UserId
            WHERE w.WorkItemId = @WorkItemId AND w.DeletedAt IS NULL
            """,
            new { UserId = userId, WorkItemId = workItemId }, transaction,
            cancellationToken: cancellationToken));

    private static async Task ValidateAssignedUserAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid? assignedUserId,
        CancellationToken cancellationToken)
    {
        if (assignedUserId is null)
        {
            return;
        }

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1 FROM dbo.Users u JOIN dbo.Accounts a ON a.UserId = u.UserId
                WHERE u.UserId = @UserId AND a.IsEnabled = 1
            ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
            """,
            new { UserId = assignedUserId.Value }, transaction, cancellationToken: cancellationToken));
        if (!exists)
        {
            throw new RequestValidationException("指派使用者不存在或已停用。");
        }
    }

    private static async Task<bool> WorkItemExistsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid workItemId,
        CancellationToken cancellationToken) => await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.WorkItems WHERE WorkItemId = @WorkItemId AND DeletedAt IS NULL) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END",
            new { WorkItemId = workItemId }, transaction, cancellationToken: cancellationToken));

    private static Task InsertHistoryAsync(
        DbConnection connection,
        DbTransaction transaction,
        WorkItemMutationRow row,
        string actionCode,
        Guid changedByUserId,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken) => connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.WorkItemHistories
            (
                WorkItemId, ActionId, ChangedByUserId, ChangedAt, SnapshotTitle, SnapshotDescription,
                SnapshotCreatedByUserId, SnapshotAssignedUserId, SnapshotCreatedAt, SnapshotUpdatedAt,
                SnapshotDeletedAt, SnapshotDeletedByUserId, SourceRowVersion
            )
            VALUES
            (
                @WorkItemId, @ActionId, @ChangedByUserId, @ChangedAt, @Title, @Description,
                @CreatedByUserId, @AssignedUserId, @CreatedAt, @UpdatedAt,
                @DeletedAt, @DeletedByUserId, @RowVersion
            )
            """,
            new
            {
                row.WorkItemId,
                ActionId = ActionIds[actionCode],
                ChangedByUserId = changedByUserId,
                ChangedAt = changedAt,
                row.Title,
                row.Description,
                row.CreatedByUserId,
                row.AssignedUserId,
                row.CreatedAt,
                row.UpdatedAt,
                row.DeletedAt,
                row.DeletedByUserId,
                row.RowVersion
            }, transaction, cancellationToken: cancellationToken));

    private static WorkItemResponse Map(WorkItemRow row) => new(
        row.WorkItemId,
        row.Title,
        row.Description,
        row.CreatedByUserId,
        row.AssignedUserId,
        row.CreatedAt,
        row.UpdatedAt,
        row.StatusCode,
        row.StatusCode == WorkItemStatusCodes.Confirm,
        row.ConfirmedAt,
        Convert.ToBase64String(row.RowVersion));

    private static WorkItemResponse Map(WorkItemMutationRow row, string statusCode, DateTimeOffset? confirmedAt) => new(
        row.WorkItemId,
        row.Title,
        row.Description,
        row.CreatedByUserId,
        row.AssignedUserId,
        row.CreatedAt,
        row.UpdatedAt,
        statusCode,
        statusCode == WorkItemStatusCodes.Confirm,
        confirmedAt,
        Convert.ToBase64String(row.RowVersion));

    private static void ValidateQuery(WorkItemQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 ||
            !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("分頁或排序參數無效。");
        }
    }

    private static void ValidateWorkItem(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
        {
            throw new RequestValidationException("Title 必填且最多 200 字元。");
        }
    }

    private sealed record WorkItemRow(
        Guid WorkItemId,
        string Title,
        string? Description,
        Guid CreatedByUserId,
        Guid? AssignedUserId,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        byte[] RowVersion,
        DateTimeOffset? ConfirmedAt,
        string StatusCode);

    private sealed record WorkItemMutationRow(
        Guid WorkItemId,
        string Title,
        string? Description,
        Guid CreatedByUserId,
        Guid? AssignedUserId,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? DeletedAt,
        Guid? DeletedByUserId,
        byte[] RowVersion);
}
