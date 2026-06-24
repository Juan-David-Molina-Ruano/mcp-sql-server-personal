using Microsoft.Data.SqlClient;

namespace McpSqlServerPersonal;

/// <summary>
/// Creates <see cref="SqlConnection"/> instances from the server configuration.
/// </summary>
public sealed class SqlConnectionFactory(ServerConfig config)
{
    /// <summary>
    /// Creates a new SQL connection using the configured connection string.
    /// The caller is responsible for opening and disposing the connection.
    /// </summary>
    public SqlConnection CreateConnection()
    {
        return new SqlConnection(config.BuildConnectionString());
    }
}
