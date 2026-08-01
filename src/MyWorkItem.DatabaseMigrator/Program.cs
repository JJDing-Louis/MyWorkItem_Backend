namespace MyWorkItem.DatabaseMigrator;

public static class Program
{
    public static async Task<int> Main()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("缺少 ConnectionStrings__DefaultConnection。");
            return 2;
        }

        var result = DatabaseUpgrade.Run(connectionString);
        if (!result.Successful)
        {
            Console.Error.WriteLine(result.Error);
            return 1;
        }

        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        await SeedData.ApplyAsync(connectionString, environmentName);
        Console.WriteLine("資料庫 Migration 與種子資料已完成。");
        return 0;
    }
}
