namespace McpSqlServerPersonal.Security;

/// <summary>
/// Detects sensitive columns by semantic name patterns.
/// Operates on generic column names — no database-engine specifics.
///
/// Heuristic:
/// <list type="bullet">
/// <item><strong>Strong patterns</strong> (e.g. password, hash, token) are high-confidence indicators of sensitive data. They trigger even when no trustworthy lineage is available, because an alias like <c>HashVisible</c> or <c>PasswordHint</c> is very likely derived from sensitive source data.</item>
/// <item><strong>Broad patterns</strong> (e.g. apikey, api_key) are noisy and easily produce false positives (e.g. <c>CustomerID AS ApiKeyName</c>). They require trustworthy lineage (base column metadata) before they trigger obfuscation.</item>
/// </list>
/// </summary>
public sealed class SensitiveColumnPolicy
{
    private readonly HashSet<string> _strongPatterns;
    private readonly HashSet<string> _broadPatterns;

    public SensitiveColumnPolicy()
        : this(DefaultStrongPatterns, DefaultBroadPatterns)
    {
    }

    /// <summary>
    /// Legacy constructor for backward compatibility. Treats all supplied patterns as strong.
    /// </summary>
    public SensitiveColumnPolicy(IEnumerable<string>? patterns)
        : this(patterns, Array.Empty<string>())
    {
    }

    public SensitiveColumnPolicy(IEnumerable<string>? strongPatterns, IEnumerable<string>? broadPatterns)
    {
        _strongPatterns = new HashSet<string>(
            strongPatterns ?? DefaultStrongPatterns,
            StringComparer.OrdinalIgnoreCase);
        _broadPatterns = new HashSet<string>(
            broadPatterns ?? DefaultBroadPatterns,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// High-confidence patterns that trigger even without trustworthy lineage.
    /// These represent semantic concepts that are almost always sensitive in a database context.
    /// </summary>
    public static IEnumerable<string> DefaultStrongPatterns => new[]
    {
        "password", "passwd", "pwd",
        "hash", "salt",
        "token", "secret",
        "ssn", "social_security", "socialsecurity",
        "credit_card", "creditcard", "cvv", "cvc",
        "iban", "routing_number", "routingnumber",
        "private_key", "privatekey",
    };

    /// <summary>
    /// Low-confidence patterns that require trustworthy lineage to avoid false positives.
    /// These are noisy when applied to aliases or expression names (e.g. CustomerID AS ApiKeyName).
    /// </summary>
    public static IEnumerable<string> DefaultBroadPatterns => new[]
    {
        "api_key", "apikey",
        "pin",
    };

    /// <summary>
    /// Returns true if the column should be treated as sensitive.
    /// </summary>
    public bool IsSensitive(string columnName, string? baseColumnName = null)
        => IsSensitive(columnName, baseColumnName, out _);

    /// <summary>
    /// Returns true if the column should be treated as sensitive, with a human-readable reason.
    ///
    /// When <paramref name="baseColumnName"/> is provided (trustworthy lineage available),
    /// both strong and broad patterns are evaluated against the base column name.
    ///
    /// When <paramref name="baseColumnName"/> is absent (no trustworthy lineage), only
    /// strong patterns are evaluated against the display/alias name. Broad patterns are
    /// deliberately ignored to avoid false positives on innocent aliases.
    /// </summary>
    public bool IsSensitive(string columnName, string? baseColumnName, out string? reason)
    {
        // Trustworthy source metadata available — evaluate both strong and broad patterns
        // against the real column name.
        if (!string.IsNullOrWhiteSpace(baseColumnName))
        {
            string? matched = FindMatchingPattern(baseColumnName, _strongPatterns);
            if (matched is not null)
            {
                reason = $"base column matches strong pattern '{matched}'";
                return true;
            }

            matched = FindMatchingPattern(baseColumnName, _broadPatterns);
            if (matched is not null)
            {
                reason = $"base column matches broad pattern '{matched}'";
                return true;
            }

            reason = "base column not sensitive";
            return false;
        }

        // No trustworthy lineage available. Fall back to strong patterns only
        // against the display/alias name. Broad patterns are too noisy without
        // lineage (e.g. CustomerID AS ApiKeyName).
        string? aliasMatched = FindMatchingPattern(columnName, _strongPatterns);
        if (aliasMatched is not null)
        {
            reason = $"alias matches strong pattern '{aliasMatched}' (no trustworthy lineage)";
            return true;
        }

        reason = "no trustworthy lineage; broad patterns require lineage";
        return false;
    }

    private static string? FindMatchingPattern(string text, HashSet<string> patterns)
    {
        foreach (string pattern in patterns)
        {
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return pattern;
        }
        return null;
    }
}
