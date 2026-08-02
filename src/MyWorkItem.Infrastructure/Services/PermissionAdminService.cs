using System.Data.Common;
using Dapper;
using MyWorkItem.Application.Abstractions;
using MyWorkItem.Application.Contracts;
using MyWorkItem.Application.Exceptions;

namespace MyWorkItem.Infrastructure.Services;

public sealed class PermissionAdminService(IDbConnectionFactory connectionFactory, IClock clock)
    : IPermissionAdminService
{
    public Task<IReadOnlyCollection<PermissionItemResponse>> GetRolesAsync(CancellationToken cancellationToken) =>
        GetItemsAsync(true, cancellationToken);

    public Task<IReadOnlyCollection<PermissionItemResponse>> GetFunctionsAsync(CancellationToken cancellationToken) =>
        GetItemsAsync(false, cancellationToken);

    public Task<PermissionItemResponse> CreateRoleAsync(CreatePermissionItemRequest request, CancellationToken cancellationToken) =>
        CreateAsync(true, request, cancellationToken);

    public Task<PermissionItemResponse> CreateFunctionAsync(CreatePermissionItemRequest request, CancellationToken cancellationToken) =>
        CreateAsync(false, request, cancellationToken);

    public Task<PermissionItemResponse> UpdateRoleAsync(Guid roleId, UpdatePermissionItemRequest request, CancellationToken cancellationToken) =>
        UpdateAsync(true, roleId, request, cancellationToken);

    public Task<PermissionItemResponse> UpdateFunctionAsync(Guid functionId, UpdatePermissionItemRequest request, CancellationToken cancellationToken) =>
        UpdateAsync(false, functionId, request, cancellationToken);

    public async Task ReplaceRoleFunctionsAsync(
        Guid currentUserId,
        Guid roleId,
        IReadOnlyCollection<Guid> functionIds,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (!await ExistsAsync(connection, transaction, "Roles", "RoleId", roleId, cancellationToken))
        {
            throw new NotFoundException("找不到角色。");
        }

        var distinct = functionIds.Distinct().ToArray();
        var count = distinct.Length == 0 ? 0 : await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.Functions WHERE FunctionId IN @Ids AND IsEnabled = 1",
            new { Ids = distinct }, transaction, cancellationToken: cancellationToken));
        if (count != distinct.Length)
        {
            throw new RequestValidationException("Function 不存在或已停用。");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE dbo.RoleFunctions WHERE RoleId = @RoleId",
            new { RoleId = roleId }, transaction, cancellationToken: cancellationToken));
        foreach (var functionId in distinct)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT dbo.RoleFunctions (RoleId, FunctionId, IsEnabled, UpdatedAt, UpdatedByUserId) VALUES (@RoleId, @FunctionId, 1, @Now, @UserId)",
                new { RoleId = roleId, FunctionId = functionId, Now = clock.UtcNow, UserId = currentUserId }, transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<PermissionItemResponse>> GetItemsAsync(
        bool roles,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        if (!roles)
        {
            return (await connection.QueryAsync<PermissionItemResponse>(new CommandDefinition(
                "SELECT FunctionId AS Id, Code, Name, Description, IsEnabled FROM dbo.Functions ORDER BY Code",
                cancellationToken: cancellationToken))).ToArray();
        }

        var rows = await connection.QueryAsync<ItemRow>(new CommandDefinition(
            "SELECT RoleId AS Id, Code, Name, Description, IsEnabled FROM dbo.Roles ORDER BY Code",
            cancellationToken: cancellationToken));
        var result = new List<PermissionItemResponse>();
        foreach (var row in rows)
        {
            var functions = (await connection.QueryAsync<LookupResponse>(new CommandDefinition(
                """
                SELECT f.FunctionId AS Id, f.Code, f.Name, f.IsEnabled
                FROM dbo.RoleFunctions rf JOIN dbo.Functions f ON f.FunctionId = rf.FunctionId
                WHERE rf.RoleId = @RoleId AND rf.IsEnabled = 1 ORDER BY f.Code
                """,
                new { RoleId = row.Id }, cancellationToken: cancellationToken))).ToArray();
            result.Add(new PermissionItemResponse(row.Id, row.Code, row.Name, row.Description, row.IsEnabled, functions));
        }

        return result;
    }

    private async Task<PermissionItemResponse> CreateAsync(
        bool role,
        CreatePermissionItemRequest request,
        CancellationToken cancellationToken)
    {
        var table = role ? "Roles" : "Functions";
        var idColumn = role ? "RoleId" : "FunctionId";
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            $"SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.{table} WHERE Code = @Code) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END",
            new { Code = request.Code.Trim() }, cancellationToken: cancellationToken));
        if (exists)
        {
            throw new ConflictException("Code 已存在。");
        }

        var id = Guid.NewGuid();
        var now = clock.UtcNow;
        await connection.ExecuteAsync(new CommandDefinition(
            $"INSERT dbo.{table} ({idColumn}, Code, Name, Description, IsEnabled, CreatedAt, UpdatedAt) VALUES (@Id, @Code, @Name, @Description, 1, @Now, @Now)",
            new { Id = id, Code = request.Code.Trim(), Name = request.Name.Trim(), request.Description, Now = now },
            cancellationToken: cancellationToken));
        return new PermissionItemResponse(id, request.Code.Trim(), request.Name.Trim(), request.Description, true);
    }

    private async Task<PermissionItemResponse> UpdateAsync(
        bool role,
        Guid id,
        UpdatePermissionItemRequest request,
        CancellationToken cancellationToken)
    {
        var table = role ? "Roles" : "Functions";
        var idColumn = role ? "RoleId" : "FunctionId";
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            $"UPDATE dbo.{table} SET Name = @Name, Description = @Description, IsEnabled = @IsEnabled, UpdatedAt = @Now WHERE {idColumn} = @Id",
            new { Id = id, Name = request.Name.Trim(), request.Description, request.IsEnabled, Now = clock.UtcNow },
            cancellationToken: cancellationToken));
        if (affected == 0)
        {
            throw new NotFoundException(role ? "找不到角色。" : "找不到 Function。");
        }

        var row = await connection.QuerySingleAsync<ItemRow>(new CommandDefinition(
            $"SELECT {idColumn} AS Id, Code, Name, Description, IsEnabled FROM dbo.{table} WHERE {idColumn} = @Id",
            new { Id = id }, cancellationToken: cancellationToken));
        return new PermissionItemResponse(row.Id, row.Code, row.Name, row.Description, row.IsEnabled);
    }

    private static Task<bool> ExistsAsync(
        DbConnection connection,
        DbTransaction transaction,
        string table,
        string column,
        Guid id,
        CancellationToken cancellationToken) => connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            $"SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.{table} WHERE {column} = @Id) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END",
            new { Id = id }, transaction, cancellationToken: cancellationToken));

    private sealed record ItemRow(Guid Id, string Code, string Name, string? Description, bool IsEnabled);
}
