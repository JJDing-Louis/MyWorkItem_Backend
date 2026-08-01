using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using MyWorkItem.Application;

namespace MyWorkItem.Infrastructure;

public sealed class AdministrationRepository(IDbConnectionFactory connections) : IAdministrationRepository
{
    public async Task<PagedResult<UserResponse>> ListUsersAsync(int page, int pageSize, string? keyword, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        const string sql = """
            SELECT u.UserId
            FROM Users u
            INNER JOIN Accounts a ON a.AccountId = u.AccountId
            WHERE @Keyword IS NULL OR a.UserName LIKE '%' + @Keyword + '%' OR u.Name LIKE '%' + @Keyword + '%'
            ORDER BY a.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1)
            FROM Users u
            INNER JOIN Accounts a ON a.AccountId = u.AccountId
            WHERE @Keyword IS NULL OR a.UserName LIKE '%' + @Keyword + '%' OR u.Name LIKE '%' + @Keyword + '%';
            """;
        await using var connection = connections.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { Keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim(), Offset = (page - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var ids = (await grid.ReadAsync<Guid>()).ToArray();
        var total = await grid.ReadSingleAsync<int>();
        var users = new List<UserResponse>(ids.Length);
        foreach (var id in ids)
        {
            var user = await GetUserAsync(id, cancellationToken);
            if (user is not null)
            {
                users.Add(user);
            }
        }

        return new PagedResult<UserResponse>(users, page, pageSize, total);
    }

    public async Task<UserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT u.UserId, a.AccountId, a.UserName, u.Name, u.Email, u.Remark, a.IsEnabled,
                   r.RoleId, r.Code AS RoleCode, r.Name AS RoleName, r.IsEnabled AS RoleIsEnabled
            FROM Users u
            INNER JOIN Accounts a ON a.AccountId = u.AccountId
            LEFT JOIN AccountRoles ar ON ar.AccountId = a.AccountId
            LEFT JOIN Roles r ON r.RoleId = ar.RoleId
            WHERE u.UserId = @UserId;
            """;
        await using var connection = connections.CreateConnection();
        var rows = (await connection.QueryAsync<UserRow>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))).ToArray();
        if (rows.Length == 0)
        {
            return null;
        }

        var first = rows[0];
        return new UserResponse(
            first.UserId,
            first.AccountId,
            first.UserName,
            first.Name,
            first.Email,
            first.Remark,
            first.IsEnabled,
            rows.Where(x => x.RoleId is not null).Select(x => new NamedReference(x.RoleId!.Value, x.RoleCode!, x.RoleName!, x.RoleIsEnabled!.Value)).ToArray());
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, string passwordHash, CancellationToken cancellationToken)
    {
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var connection = connections.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await EnsureIdsExistAsync(connection, transaction, "Roles", "RoleId", request.RoleIds, cancellationToken);
            const string sql = """
                INSERT INTO Accounts (AccountId, UserName, PasswordHash, IsEnabled, CreatedAt, UpdatedAt)
                VALUES (@AccountId, @UserName, @PasswordHash, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
                INSERT INTO Users (UserId, AccountId, Name, Email, Remark)
                VALUES (@UserId, @AccountId, @Name, @Email, @Remark);
                """;
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                AccountId = accountId,
                UserId = userId,
                UserName = request.UserName.Trim(),
                PasswordHash = passwordHash,
                Name = request.Name.Trim(),
                Email = NormalizeOptional(request.Email),
                Remark = NormalizeOptional(request.Remark)
            }, transaction, cancellationToken: cancellationToken));
            await ReplaceLinksAsync(connection, transaction, "AccountRoles", "AccountId", accountId, "RoleId", request.RoleIds, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("帳號名稱或 Email 已存在。");
        }

        return await GetUserAsync(userId, cancellationToken) ?? throw new InvalidOperationException("新增使用者後無法讀回資料。");
    }

    public async Task<UserResponse> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Users
            SET Name = @Name, Email = @Email, Remark = @Remark
            WHERE UserId = @UserId;
            UPDATE Accounts SET UpdatedAt = SYSUTCDATETIME()
            WHERE AccountId = (SELECT AccountId FROM Users WHERE UserId = @UserId);
            """;
        try
        {
            await using var connection = connections.CreateConnection();
            var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                UserId = userId,
                Name = request.Name.Trim(),
                Email = NormalizeOptional(request.Email),
                Remark = NormalizeOptional(request.Remark)
            }, cancellationToken: cancellationToken));
            if (affected == 0)
            {
                throw new NotFoundException("找不到指定的使用者。");
            }
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new ConflictException("Email 已存在。");
        }

        return await GetUserAsync(userId, cancellationToken) ?? throw new NotFoundException("找不到指定的使用者。");
    }

    public async Task SetAccountStatusAsync(Guid userId, bool isEnabled, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Accounts
            SET IsEnabled = @IsEnabled, UpdatedAt = SYSUTCDATETIME()
            WHERE AccountId = (SELECT AccountId FROM Users WHERE UserId = @UserId);
            """;
        await ExecuteRequiredAsync(sql, new { UserId = userId, IsEnabled = isEnabled }, "找不到指定的使用者。", cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid userId, string passwordHash, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Accounts
            SET PasswordHash = @PasswordHash, UpdatedAt = SYSUTCDATETIME()
            WHERE AccountId = (SELECT AccountId FROM Users WHERE UserId = @UserId);
            """;
        await ExecuteRequiredAsync(sql, new { UserId = userId, PasswordHash = passwordHash }, "找不到指定的使用者。", cancellationToken);
    }

    public async Task ReplaceUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        await using var connection = connections.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var accountId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT AccountId FROM Users WHERE UserId = @UserId;", new { UserId = userId }, transaction, cancellationToken: cancellationToken));
        if (accountId is null)
        {
            throw new NotFoundException("找不到指定的使用者。");
        }

        await EnsureIdsExistAsync(connection, transaction, "Roles", "RoleId", roleIds, cancellationToken);
        await ReplaceLinksAsync(connection, transaction, "AccountRoles", "AccountId", accountId.Value, "RoleId", roleIds, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.RoleId, r.Code, r.Name, r.IsEnabled,
                   f.FunctionId, f.Code AS FunctionCode, f.Name AS FunctionName, f.IsEnabled AS FunctionIsEnabled
            FROM Roles r
            LEFT JOIN RoleFunctions rf ON rf.RoleId = r.RoleId
            LEFT JOIN [Functions] f ON f.FunctionId = rf.FunctionId
            ORDER BY r.Code, f.Code;
            """;
        await using var connection = connections.CreateConnection();
        var rows = await connection.QueryAsync<RoleRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.GroupBy(x => new { x.RoleId, x.Code, x.Name, x.IsEnabled })
            .Select(group => new RoleResponse(
                group.Key.RoleId,
                group.Key.Code,
                group.Key.Name,
                group.Key.IsEnabled,
                group.Where(x => x.FunctionId is not null).Select(x => new NamedReference(x.FunctionId!.Value, x.FunctionCode!, x.FunctionName!, x.FunctionIsEnabled!.Value)).ToArray()))
            .ToArray();
    }

    public async Task<RoleResponse> CreateRoleAsync(CreateNamedResourceRequest request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await CreateNamedAsync("Roles", "RoleId", id, request, cancellationToken);
        return (await ListRolesAsync(cancellationToken)).Single(x => x.RoleId == id);
    }

    public async Task<RoleResponse> UpdateRoleAsync(Guid roleId, UpdateNamedResourceRequest request, CancellationToken cancellationToken)
    {
        await UpdateNamedAsync("Roles", "RoleId", roleId, request, cancellationToken);
        return (await ListRolesAsync(cancellationToken)).Single(x => x.RoleId == roleId);
    }

    public async Task ReplaceRoleFunctionsAsync(Guid roleId, IReadOnlyCollection<Guid> functionIds, CancellationToken cancellationToken)
    {
        await using var connection = connections.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await EnsureIdsExistAsync(connection, transaction, "Roles", "RoleId", [roleId], cancellationToken);
        await EnsureIdsExistAsync(connection, transaction, "[Functions]", "FunctionId", functionIds, cancellationToken);
        await ReplaceLinksAsync(connection, transaction, "RoleFunctions", "RoleId", roleId, "FunctionId", functionIds, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<FunctionResponse>> ListFunctionsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT FunctionId, Code, Name, IsEnabled FROM [Functions] ORDER BY Code;";
        await using var connection = connections.CreateConnection();
        return (await connection.QueryAsync<FunctionResponse>(new CommandDefinition(sql, cancellationToken: cancellationToken))).ToArray();
    }

    public async Task<FunctionResponse> CreateFunctionAsync(CreateNamedResourceRequest request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await CreateNamedAsync("[Functions]", "FunctionId", id, request, cancellationToken);
        return (await ListFunctionsAsync(cancellationToken)).Single(x => x.FunctionId == id);
    }

    public async Task<FunctionResponse> UpdateFunctionAsync(Guid functionId, UpdateNamedResourceRequest request, CancellationToken cancellationToken)
    {
        await UpdateNamedAsync("[Functions]", "FunctionId", functionId, request, cancellationToken);
        return (await ListFunctionsAsync(cancellationToken)).Single(x => x.FunctionId == functionId);
    }

    private async Task ExecuteRequiredAsync(string sql, object parameters, string error, CancellationToken cancellationToken)
    {
        await using var connection = connections.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            throw new NotFoundException(error);
        }
    }

    private async Task CreateNamedAsync(string table, string idColumn, Guid id, CreateNamedResourceRequest request, CancellationToken cancellationToken)
    {
        var sql = $"INSERT INTO {table} ({idColumn}, Code, Name, IsEnabled) VALUES (@Id, @Code, @Name, 1);";
        try
        {
            await using var connection = connections.CreateConnection();
            await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, Code = request.Code.Trim(), Name = request.Name.Trim() }, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new ConflictException("Code 已存在。");
        }
    }

    private async Task UpdateNamedAsync(string table, string idColumn, Guid id, UpdateNamedResourceRequest request, CancellationToken cancellationToken)
    {
        var sql = $"UPDATE {table} SET Name = @Name, IsEnabled = @IsEnabled WHERE {idColumn} = @Id;";
        await ExecuteRequiredAsync(sql, new { Id = id, Name = request.Name.Trim(), request.IsEnabled }, "找不到指定的資源。", cancellationToken);
    }

    private static async Task EnsureIdsExistAsync(IDbConnection connection, IDbTransaction transaction, string table, string idColumn, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var sql = $"SELECT COUNT(1) FROM {table} WHERE {idColumn} IN @Ids;";
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Ids = ids.Distinct().ToArray() }, transaction, cancellationToken: cancellationToken));
        if (count != ids.Distinct().Count())
        {
            throw new ValidationException("指定的關聯資源不存在。");
        }
    }

    private static async Task ReplaceLinksAsync(IDbConnection connection, IDbTransaction transaction, string table, string ownerColumn, Guid ownerId, string valueColumn, IReadOnlyCollection<Guid> values, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition($"DELETE FROM {table} WHERE {ownerColumn} = @OwnerId;", new { OwnerId = ownerId }, transaction, cancellationToken: cancellationToken));
        foreach (var value in values.Distinct())
        {
            await connection.ExecuteAsync(new CommandDefinition($"INSERT INTO {table} ({ownerColumn}, {valueColumn}) VALUES (@OwnerId, @Value);", new { OwnerId = ownerId, Value = value }, transaction, cancellationToken: cancellationToken));
        }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class UserRow
    {
        public Guid UserId { get; init; }
        public Guid AccountId { get; init; }
        public required string UserName { get; init; }
        public required string Name { get; init; }
        public string? Email { get; init; }
        public string? Remark { get; init; }
        public bool IsEnabled { get; init; }
        public Guid? RoleId { get; init; }
        public string? RoleCode { get; init; }
        public string? RoleName { get; init; }
        public bool? RoleIsEnabled { get; init; }
    }

    private sealed class RoleRow
    {
        public Guid RoleId { get; init; }
        public required string Code { get; init; }
        public required string Name { get; init; }
        public bool IsEnabled { get; init; }
        public Guid? FunctionId { get; init; }
        public string? FunctionCode { get; init; }
        public string? FunctionName { get; init; }
        public bool? FunctionIsEnabled { get; init; }
    }
}
