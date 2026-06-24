namespace McpSqlServerPersonal;

/// <summary>
/// Server configuration loaded from MCP client-scoped environment variables.
/// </summary>
public sealed class ServerConfig
{
    // Required
    public string Host { get; init; } = string.Empty;
    public string Database { get; init; } = string.Empty;
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    // Optional with defaults
    public int? Port { get; init; }
    public bool Encrypt { get; init; } = true;
    public bool TrustCertificate { get; init; } = false;
    public int MaxRows { get; init; } = 100;
    public int CommandTimeoutSeconds { get; init; } = 15;
    public int MaxResponseBytes { get; init; } = 1048576;

    /// <summary>
    /// Builds a SQL Server connection string from the current configuration.
    /// </summary>
    public string BuildConnectionString()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = Port.HasValue ? $"{Host},{Port.Value}" : Host,
            InitialCatalog = Database,
            UserID = User,
            Password = Password,
            Encrypt = Encrypt,
            TrustServerCertificate = TrustCertificate,
            ConnectTimeout = 30,
        };
        return builder.ConnectionString;
    }
}
