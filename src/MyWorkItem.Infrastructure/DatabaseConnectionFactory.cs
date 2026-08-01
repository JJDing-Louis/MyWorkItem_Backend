using Microsoft.Data.SqlClient;

namespace MyWorkItem.Infrastructure;

public interface IDbConnectionFactory
{
    SqlConnection CreateConnection();
}

public sealed class SqlServerConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public SqlConnection CreateConnection() => new(connectionString);
}
