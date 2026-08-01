using DbUp;
using DbUp.Engine;

namespace MyWorkItem.DatabaseMigrator;

public static class DatabaseUpgrade
{
    public static DatabaseUpgradeResult Run(string connectionString)
    {
        EnsureDatabase.For.SqlDatabase(connectionString);
        return DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseUpgrade).Assembly)
            .LogToConsole()
            .Build()
            .PerformUpgrade();
    }
}
