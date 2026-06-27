using McpSqlServerPersonal.Security;
using Xunit;

namespace McpSqlServerPersonal.Tests;

public class ResultSetSanitizerTests
{
    private readonly ResultSetSanitizer _sanitizer = new(new SensitiveColumnPolicy(), new DataObfuscator());

    [Fact]
    public void SanitizeRows_SensitiveColumns_Obfuscated()
    {
        var columnNames = new[] { "Id", "Password", "Name" };
        var rows = new List<object[]>
        {
            new object[] { 1, "secret123", "Alice" },
            new object[] { 2, "hunter2", "Bob" },
        };

        var result = _sanitizer.SanitizeRows(columnNames, rows);

        Assert.Equal("***", result[0][1]);
        Assert.Equal("***", result[1][1]);
        Assert.Equal(1, result[0][0]);
        Assert.Equal("Alice", result[0][2]);
    }

    [Fact]
    public void SanitizeRows_NoSensitiveColumns_PreservesAll()
    {
        var columnNames = new[] { "Id", "Name" };
        var rows = new List<object[]>
        {
            new object[] { 1, "Alice" },
        };

        var result = _sanitizer.SanitizeRows(columnNames, rows);

        Assert.Equal(1, result[0][0]);
        Assert.Equal("Alice", result[0][1]);
    }

    [Fact]
    public void SanitizeRows_WithBaseColumnNames_UsesLineage()
    {
        var columnNames = new[] { "AliasName", "DisplayKey" };
        var baseColumnNames = new string?[] { "password", "api_key" };
        var rows = new List<object[]>
        {
            new object[] { "x", "y" },
        };

        var result = _sanitizer.SanitizeRows(columnNames, baseColumnNames, rows);

        // Both should be obfuscated because base columns are sensitive
        Assert.Equal("***", result[0][0]);
        Assert.Equal("***", result[0][1]);
    }

    [Fact]
    public void SanitizeRows_OriginalRows_NotMutated()
    {
        var columnNames = new[] { "Password" };
        var rows = new List<object[]>
        {
            new object[] { "secret123" },
        };

        _sanitizer.SanitizeRows(columnNames, rows);

        Assert.Equal("secret123", rows[0][0]);
    }

    [Fact]
    public void SanitizeRows_EmptyRows_ReturnsEmpty()
    {
        var columnNames = new[] { "Id", "Password" };
        var rows = new List<object[]>();

        var result = _sanitizer.SanitizeRows(columnNames, rows);

        Assert.Empty(result);
    }

    [Fact]
    public void IsSensitiveColumn_DelegatesToPolicy()
    {
        Assert.True(_sanitizer.IsSensitiveColumn("password"));
        Assert.False(_sanitizer.IsSensitiveColumn("name"));
    }
}
