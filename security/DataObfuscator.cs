namespace McpSqlServerPersonal.Security;

/// <summary>
/// Replaces values with an obfuscation marker.
/// Treats all input as untrusted data — never interprets it.
/// </summary>
public sealed class DataObfuscator
{
    public object Obfuscate(object? value) => "***";
}
