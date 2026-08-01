using System.Data;
using Dapper;
using MyWorkItem.Application;
using MyWorkItem.Domain;
using SqlKata;
using SqlKata.Compilers;

namespace MyWorkItem.Infrastructure;

public sealed class WorkItemRepository(IDbConnectionFactory connections) : IWorkItemRepository
{
    private readonly SqlServerCompiler _compiler = new();

    public async Task<PagedResult<WorkItemRecord>> ListAsync(Guid userId, int page, int pageSize, string? keyword, bool descending, CancellationToken cancellationToken)
    {
        var query = BaseQuery(userId);
        var countQuery = new Query("WorkItems as w").WhereNull("w.DeletedAt").AsCount();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query.WhereLike("w.Title", $"%{keyword}%");
            countQuery.WhereLike("w.Title", $"%{keyword}%");
        }

        if (descending)
        {
            query.OrderByDesc("w.CreatedAt");
        }
        else
        {
            query.OrderBy("w.CreatedAt");
        }

        query.Offset((page - 1) * pageSize).Limit(pageSize);

        var compiledItems = _compiler.Compile(query);
        var compiledCount = _compiler.Compile(countQuery);
        await using var connection = connections.CreateConnection();
        var items = (await connection.QueryAsync<WorkItemRecord>(new CommandDefinition(compiledItems.Sql, compiledItems.NamedBindings, cancellationToken: cancellationToken))).ToArray();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(compiledCount.Sql, compiledCount.NamedBindings, cancellationToken: cancellationToken));
        return new PagedResult<WorkItemRecord>(items, page, pageSize, total);
    }

    public async Task<WorkItemRecord?> GetAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken)
    {
        var compiled = _compiler.Compile(BaseQuery(userId).Where("w.WorkItemId", workItemId));
        await using var connection = connections.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WorkItemRecord>(new CommandDefinition(compiled.Sql, compiled.NamedBindings, cancellationToken: cancellationToken));
    }

    public async Task<WorkItemRecord> CreateAsync(string title, string? description, Guid accountId, Guid userId, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO WorkItems (WorkItemId, Title, Description, CreatedBy, CreatedAt, UpdatedAt)
            VALUES (@WorkItemId, @Title, @Description, @CreatedBy, SYSUTCDATETIME(), SYSUTCDATETIME());
            """;
        await using var connection = connections.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { WorkItemId = id, Title = title, Description = description, CreatedBy = accountId }, cancellationToken: cancellationToken));
        return await GetAsync(id, userId, cancellationToken) ?? throw new InvalidOperationException("新增 Work Item 後無法讀回資料。");
    }

    public async Task<WorkItemRecord> UpdateAsync(Guid workItemId, string title, string? description, byte[] rowVersion, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE WorkItems
            SET Title = @Title, Description = @Description, UpdatedAt = SYSUTCDATETIME()
            WHERE WorkItemId = @WorkItemId AND RowVersion = @RowVersion AND DeletedAt IS NULL;
            """;
        await using var connection = connections.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new { WorkItemId = workItemId, Title = title, Description = description, RowVersion = rowVersion }, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            const string existsSql = "SELECT COUNT(1) FROM WorkItems WHERE WorkItemId = @WorkItemId AND DeletedAt IS NULL;";
            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(existsSql, new { WorkItemId = workItemId }, cancellationToken: cancellationToken));
            if (exists == 0)
            {
                throw new NotFoundException("找不到指定的 Work Item。");
            }

            throw new ConflictException("Work Item 已被其他使用者更新，請重新載入後再試。");
        }

        return await GetAsync(workItemId, userId, cancellationToken) ?? throw new NotFoundException("找不到指定的 Work Item。");
    }

    public async Task DeleteAsync(Guid workItemId, Guid accountId, byte[]? rowVersion, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE WorkItems
            SET DeletedAt = SYSUTCDATETIME(), DeletedBy = @DeletedBy, UpdatedAt = SYSUTCDATETIME()
            WHERE WorkItemId = @WorkItemId AND DeletedAt IS NULL;
            """;
        await using var connection = connections.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new { WorkItemId = workItemId, DeletedBy = accountId }, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            throw new NotFoundException("找不到指定的 Work Item。");
        }
    }

    public Task ConfirmAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken) =>
        SetConfirmationAsync(workItemId, userId, true, cancellationToken);

    public Task RevokeConfirmationAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken) =>
        SetConfirmationAsync(workItemId, userId, false, cancellationToken);

    public async Task ConfirmBatchAsync(IReadOnlyCollection<Guid> workItemIds, Guid userId, CancellationToken cancellationToken)
    {
        if (workItemIds.Count == 0)
        {
            throw new ValidationException("至少需要一個 WorkItemId。");
        }

        await using var connection = connections.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        const string countSql = "SELECT COUNT(1) FROM WorkItems WHERE WorkItemId IN @Ids AND DeletedAt IS NULL;";
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, new { Ids = workItemIds }, transaction, cancellationToken: cancellationToken));
        if (count != workItemIds.Count)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new NotFoundException("批次資料包含不存在或已刪除的 Work Item。");
        }

        foreach (var id in workItemIds)
        {
            await ExecuteConfirmationAsync(connection, transaction, id, userId, true, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private Query BaseQuery(Guid userId) => new Query("WorkItems as w")
        .LeftJoin("UserWorkItemStates as s", join => join.On("s.WorkItemId", "w.WorkItemId").Where("s.UserId", userId))
        .WhereNull("w.DeletedAt")
        .Select("w.WorkItemId", "w.Title", "w.Description", "w.CreatedBy", "w.CreatedAt", "w.UpdatedAt", "w.RowVersion")
        .SelectRaw("CAST(COALESCE(s.IsConfirmed, 0) AS bit) AS IsConfirmed, s.ConfirmedAt");

    private async Task SetConfirmationAsync(Guid workItemId, Guid userId, bool isConfirmed, CancellationToken cancellationToken)
    {
        await using var connection = connections.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        const string existsSql = "SELECT COUNT(1) FROM WorkItems WHERE WorkItemId = @WorkItemId AND DeletedAt IS NULL;";
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(existsSql, new { WorkItemId = workItemId }, transaction, cancellationToken: cancellationToken));
        if (exists == 0)
        {
            throw new NotFoundException("找不到指定的 Work Item。");
        }

        await ExecuteConfirmationAsync(connection, transaction, workItemId, userId, isConfirmed, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static Task<int> ExecuteConfirmationAsync(IDbConnection connection, IDbTransaction transaction, Guid workItemId, Guid userId, bool isConfirmed, CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE UserWorkItemStates WITH (HOLDLOCK) AS target
            USING (SELECT @UserId AS UserId, @WorkItemId AS WorkItemId) AS source
            ON target.UserId = source.UserId AND target.WorkItemId = source.WorkItemId
            WHEN MATCHED THEN
                UPDATE SET IsConfirmed = @IsConfirmed,
                           ConfirmedAt = CASE WHEN @IsConfirmed = 1 THEN COALESCE(target.ConfirmedAt, SYSUTCDATETIME()) ELSE NULL END,
                           UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (UserId, WorkItemId, IsConfirmed, ConfirmedAt, UpdatedAt)
                VALUES (@UserId, @WorkItemId, @IsConfirmed, CASE WHEN @IsConfirmed = 1 THEN SYSUTCDATETIME() ELSE NULL END, SYSUTCDATETIME());
            """;
        return connection.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, WorkItemId = workItemId, IsConfirmed = isConfirmed }, transaction, cancellationToken: cancellationToken));
    }
}
