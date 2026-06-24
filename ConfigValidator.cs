namespace McpSqlServerPersonal;

/// <summary>
/// Validates <see cref="ServerConfig"/> loaded from environment variables.
/// </summary>
public static class ConfigValidator
{
    public static (ServerConfig? Config, List<string> Errors) LoadAndValidate()
    {
        var errors = new List<string>();

        // Required
        string host = GetEnvOrError("SQL_SERVER_HOST", errors);
        string database = GetEnvOrError("SQL_SERVER_DATABASE", errors);
        string user = GetEnvOrError("SQL_SERVER_USER", errors);
        string password = GetEnvOrError("SQL_SERVER_PASSWORD", errors);

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        // Optional with parsing
        int? port = ParseNullableInt("SQL_SERVER_PORT", errors);
        bool encrypt = ParseBoolOrDefault("SQL_SERVER_ENCRYPT", true, errors);
        bool trustCertificate = ParseBoolOrDefault("SQL_SERVER_TRUST_CERTIFICATE", false, errors);
        int maxRows = ParseIntOrDefault("SQL_MCP_MAX_ROWS", 100, errors);
        int commandTimeout = ParseIntOrDefault("SQL_MCP_COMMAND_TIMEOUT_SECONDS", 15, errors);
        int maxResponseBytes = ParseIntOrDefault("SQL_MCP_MAX_RESPONSE_BYTES", 1048576, errors);

        // Range validations
        if (port.HasValue && (port.Value <= 0 || port.Value > 65535))
        {
            errors.Add("SQL_SERVER_PORT must be between 1 and 65535.");
        }
        if (maxRows <= 0)
        {
            errors.Add("SQL_MCP_MAX_ROWS must be greater than 0.");
        }
        if (commandTimeout <= 0)
        {
            errors.Add("SQL_MCP_COMMAND_TIMEOUT_SECONDS must be greater than 0.");
        }
        if (maxResponseBytes <= 0)
        {
            errors.Add("SQL_MCP_MAX_RESPONSE_BYTES must be greater than 0.");
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var config = new ServerConfig
        {
            Host = host,
            Database = database,
            User = user,
            Password = password,
            Port = port,
            Encrypt = encrypt,
            TrustCertificate = trustCertificate,
            MaxRows = maxRows,
            CommandTimeoutSeconds = commandTimeout,
            MaxResponseBytes = maxResponseBytes,
        };

        return (config, errors);
    }

    private static string GetEnvOrError(string name, List<string> errors)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Missing required environment variable: {name}");
            return string.Empty;
        }
        return value;
    }

    private static int ParseIntOrDefault(string name, int defaultValue, List<string> errors)
    {
        string? raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }
        if (!int.TryParse(raw, out int value))
        {
            errors.Add($"{name} must be an integer. Received: '{raw}'");
            return defaultValue;
        }
        return value;
    }

    private static bool ParseBoolOrDefault(string name, bool defaultValue, List<string> errors)
    {
        string? raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }
        if (!bool.TryParse(raw, out bool value))
        {
            errors.Add($"{name} must be a boolean (true/false). Received: '{raw}'");
            return defaultValue;
        }
        return value;
    }

    private static int? ParseNullableInt(string name, List<string> errors)
    {
        string? raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        if (!int.TryParse(raw, out int value))
        {
            errors.Add($"{name} must be an integer. Received: '{raw}'");
            return null;
        }
        return value;
    }
}
