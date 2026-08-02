using System.Data.Common;

namespace MyWorkItem.Application.Abstractions;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
