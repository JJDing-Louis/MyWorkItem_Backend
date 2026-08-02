using System.Data.Common;
using Microsoft.Data.SqlClient;
using MyWorkItem.Application.Abstractions;

namespace MyWorkItem.Infrastructure.Data;

public sealed class SqlConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public DbConnection CreateConnection() => new SqlConnection(connectionString);
}
