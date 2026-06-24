namespace McpSqlServerPersonal.Security;

/// <summary>
/// Applies <see cref="SensitiveColumnPolicy"/> to generic tabular result sets.
/// Database-engine agnostic: operates on column names and row values only.
/// </summary>
public sealed class ResultSetSanitizer(SensitiveColumnPolicy policy, DataObfuscator obfuscator)
{
    /// <summary>
    /// Checks whether a single column name is classified as sensitive.
    /// </summary>
    public bool IsSensitiveColumn(string columnName, string? baseColumnName = null)
        => policy.IsSensitive(columnName, baseColumnName);

    /// <summary>
    /// Checks whether a single column name is classified as sensitive, with a human-readable reason.
    /// </summary>
    public bool IsSensitiveColumn(string columnName, string? baseColumnName, out string? reason)
        => policy.IsSensitive(columnName, baseColumnName, out reason);

    /// <summary>
    /// Obfuscates cell values in columns flagged as sensitive.
    /// Returns a new list of rows; the original rows are not mutated.
    /// </summary>
    public List<object[]> SanitizeRows(IReadOnlyList<string> columnNames, List<object[]> rows)
        => SanitizeRows(columnNames, baseColumnNames: null, rows);

    /// <summary>
    /// Obfuscates cell values in columns flagged as sensitive, using optional base column names
    /// to distinguish between the source column and an alias/expression name.
    /// Returns a new list of rows; the original rows are not mutated.
    /// </summary>
    public List<object[]> SanitizeRows(
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string?>? baseColumnNames,
        List<object[]> rows)
    {
        var sensitive = new bool[columnNames.Count];
        for (int i = 0; i < columnNames.Count; i++)
        {
            string? baseName = baseColumnNames is not null && i < baseColumnNames.Count
                ? baseColumnNames[i]
                : null;
            sensitive[i] = policy.IsSensitive(columnNames[i], baseName);
        }

        var result = new List<object[]>(rows.Count);
        foreach (var row in rows)
        {
            var newRow = new object[row.Length];
            for (int c = 0; c < row.Length; c++)
            {
                bool isSensitive = c < sensitive.Length && sensitive[c];
                newRow[c] = isSensitive ? obfuscator.Obfuscate(row[c]) : row[c];
            }
            result.Add(newRow);
        }

        return result;
    }
}
