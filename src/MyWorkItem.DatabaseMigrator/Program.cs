using MyWorkItem.DatabaseMigrator;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("缺少 ConnectionStrings__DefaultConnection。");

var result = DatabaseMigrationRunner.Run(connectionString);
if (!result.Successful)
{
    Console.Error.WriteLine(result.Error);
    return 1;
}

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
if (environment is "Development" or "Test")
{
    await DevelopmentSeeder.SeedAsync(connectionString);
}

Console.WriteLine("資料庫 Migration 已完成。");
return 0;
