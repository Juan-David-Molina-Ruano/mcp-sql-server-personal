using System.Text.RegularExpressions;

namespace McpSqlServerPersonal;

/// <summary>
/// Validates SQL queries for read-only safety before execution.
/// </summary>
public static class QueryValidator
{
    private static readonly HashSet<string> DangerousKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "INSERT", "UPDATE", "DELETE", "MERGE", "DROP", "ALTER", "TRUNCATE",
        "CREATE", "GRANT", "REVOKE", "DENY", "INTO",
    };

    private static readonly HashSet<string> DangerousPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "sp_", "xp_",
    };

    /// <summary>
    /// Validates that a query is safe read-only SQL.
    /// Returns (true, null) if valid, otherwise (false, errorMessage).
    /// </summary>
    public static (bool IsValid, string? Error) ValidateReadOnlyQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return (false, "Query is empty or whitespace.");
        }

        string normalized = NormalizeQuery(query);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return (false, "Query contains no executable content after removing comments.");
        }

        // Reject multiple statements by checking for semicolons that separate statements.
        if (HasMultipleStatements(normalized))
        {
            return (false, "Multiple statements are not allowed.");
        }

        // Must start with an allowed keyword.
        if (!StartsWithAllowedKeyword(normalized))
        {
            return (false, "Only SELECT and WITH ... SELECT queries are allowed.");
        }

        // Reject dangerous keywords.
        string? blockedKeyword = FindBlockedKeyword(normalized);
        if (blockedKeyword is not null)
        {
            return (false, $"Query contains disallowed keyword or pattern: {blockedKeyword}");
        }

        // Reject dangerous prefixes (sp_, xp_).
        string? blockedPrefix = FindBlockedPrefix(normalized);
        if (blockedPrefix is not null)
        {
            return (false, $"Query contains disallowed prefix: {blockedPrefix}");
        }

        return (true, null);
    }

    /// <summary>
    /// Removes SQL comments, strips string literal contents, and collapses whitespace
    /// so keyword checks are reliable without false positives from literal text.
    /// </summary>
    private static string NormalizeQuery(string query)
    {
        var result = new System.Text.StringBuilder(query.Length);
        int i = 0;
        bool inString = false;

        while (i < query.Length)
        {
            char c = query[i];

            if (inString)
            {
                if (c == '\'')
                {
                    // Check for escaped single quote ('')
                    if (i + 1 < query.Length && query[i + 1] == '\'')
                    {
                        i += 2;
                        continue;
                    }
                    inString = false;
                }
                i++;
                continue;
            }

            if (c == '\'')
            {
                inString = true;
                i++;
                continue;
            }

            // Single-line comment --
            if (c == '-' && i + 1 < query.Length && query[i + 1] == '-')
            {
                // Skip until end of line
                while (i < query.Length && query[i] != '\n')
                {
                    i++;
                }
                // Preserve a single space in place of the comment to avoid token merging
                result.Append(' ');
                continue;
            }

            // Multi-line comment /* */
            if (c == '/' && i + 1 < query.Length && query[i + 1] == '*')
            {
                i += 2; // skip /*
                while (i < query.Length)
                {
                    if (query[i] == '*' && i + 1 < query.Length && query[i + 1] == '/')
                    {
                        i += 2;
                        break;
                    }
                    i++;
                }
                result.Append(' ');
                continue;
            }

            result.Append(c);
            i++;
        }

        // Collapse whitespace to single spaces for easier word-boundary checks.
        string collapsed = Regex.Replace(result.ToString(), @"\s+", " ").Trim();
        return collapsed;
    }

    private static bool HasMultipleStatements(string normalized)
    {
        // Split by semicolons. If there is more than one non-empty part, reject.
        var parts = normalized.Split(';', StringSplitOptions.RemoveEmptyEntries);
        int nonEmptyCount = 0;
        foreach (string part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                nonEmptyCount++;
                if (nonEmptyCount > 1)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool StartsWithAllowedKeyword(string normalized)
    {
        // After normalization, the query should start with SELECT or WITH.
        // Use word-boundary matching so SELECT* and SELECT(1) are accepted.
        return Regex.IsMatch(normalized, @"^(SELECT|WITH)\b", RegexOptions.IgnoreCase);
    }

    private static string? FindBlockedKeyword(string normalized)
    {
        // Use word-boundary matching to reduce false positives on identifiers like "UpdateLog".
        // However, because SQL allows identifiers without quotes, we also do a simpler
        // space-delimited token scan as a safety net.
        foreach (string keyword in DangerousKeywords)
        {
            // Regex word-boundary check
            string pattern = $@"\b{Regex.Escape(keyword)}\b";
            if (Regex.IsMatch(normalized, pattern, RegexOptions.IgnoreCase))
            {
                return keyword;
            }
        }
        return null;
    }

    private static string? FindBlockedPrefix(string normalized)
    {
        // Tokenize by splitting on whitespace and common delimiters.
        var tokens = normalized.Split(new[] { ' ', '\t', '(', ')', ',', ';', '.', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string token in tokens)
        {
            foreach (string prefix in DangerousPrefixes)
            {
                if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return prefix;
                }
            }
        }
        return null;
    }
}
