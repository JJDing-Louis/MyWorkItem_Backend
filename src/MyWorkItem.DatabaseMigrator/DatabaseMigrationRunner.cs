using DbUp;
using DbUp.Engine;

namespace MyWorkItem.DatabaseMigrator;

public static class DatabaseMigrationRunner
{
    public static DatabaseUpgradeResult Run(string connectionString)
    {
        EnsureDatabase.For.SqlDatabase(connectionString);
        return DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrationRunner).Assembly)
            .LogToConsole()
            .Build()
            .PerformUpgrade();
    }
}
