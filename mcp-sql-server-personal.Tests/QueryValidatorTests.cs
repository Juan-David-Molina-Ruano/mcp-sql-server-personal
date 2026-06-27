namespace McpSqlServerPersonal.Tests;

using Xunit;

public class QueryValidatorTests
{
    [Theory]
    [InlineData("SELECT 1", true)]
    [InlineData("SELECT * FROM Users", true)]
    [InlineData("SELECT Id, Name FROM dbo.Users WHERE Id = 1", true)]
    [InlineData("WITH cte AS (SELECT 1 AS n) SELECT n FROM cte", true)]
    [InlineData("SELECT TOP 10 * FROM Orders", true)]
    [InlineData("SELECT  -- comment\n1", true)]
    [InlineData("/* header */ SELECT 1", true)]
    [InlineData("SELECT 'DELETE' AS action", true)]
    [InlineData("SELECT 'DROP TABLE' AS warning", true)]
    [InlineData("SELECT * FROM UpdateLog", true)]
    [InlineData("SELECT*FROM Users", true)]
    [InlineData("SELECT(1)", true)]
    public void ValidQueries(string query, bool expectedValid)
    {
        var (isValid, error) = QueryValidator.ValidateReadOnlyQuery(query);
        Assert.Equal(expectedValid, isValid);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("", "Query is empty or whitespace.")]
    [InlineData("   ", "Query is empty or whitespace.")]
    [InlineData("-- comment only", "Query contains no executable content after removing comments.")]
    [InlineData("/* comment */", "Query contains no executable content after removing comments.")]
    [InlineData("INSERT INTO Users VALUES (1)", "Only SELECT and WITH")]
    [InlineData("UPDATE Users SET Name = 'x'", "Only SELECT and WITH")]
    [InlineData("DELETE FROM Users", "Only SELECT and WITH")]
    [InlineData("MERGE INTO Target USING Source ON Target.Id = Source.Id", "Only SELECT and WITH")]
    [InlineData("DROP TABLE Users", "Only SELECT and WITH")]
    [InlineData("ALTER TABLE Users ADD COLUMN x INT", "Only SELECT and WITH")]
    [InlineData("TRUNCATE TABLE Users", "Only SELECT and WITH")]
    [InlineData("CREATE TABLE Users (Id INT)", "Only SELECT and WITH")]
    [InlineData("GRANT SELECT ON Users TO public", "Only SELECT and WITH")]
    [InlineData("REVOKE SELECT ON Users FROM public", "Only SELECT and WITH")]
    [InlineData("DENY SELECT ON Users TO public", "Only SELECT and WITH")]
    [InlineData("SELECT 1; SELECT 2", "Multiple statements are not allowed.")]
    [InlineData("SELECT 1; DROP TABLE Users", "Multiple statements are not allowed.")]
    [InlineData("sp_who", "Only SELECT and WITH")]
    [InlineData("xp_cmdshell", "Only SELECT and WITH")]
    [InlineData("EXEC sp_who", "Only SELECT and WITH")]
    public void InvalidQueries(string query, string expectedFragment)
    {
        var (isValid, error) = QueryValidator.ValidateReadOnlyQuery(query);
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains(expectedFragment, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StringLiteral_DangerousKeyword_Allowed()
    {
        var (isValid, error) = QueryValidator.ValidateReadOnlyQuery("SELECT 'INSERT INTO' AS hint");
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void CommentBlock_DangerousKeyword_Allowed()
    {
        var (isValid, error) = QueryValidator.ValidateReadOnlyQuery("SELECT /* DELETE */ 1");
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void SingleLineComment_DangerousKeyword_Allowed()
    {
        var (isValid, error) = QueryValidator.ValidateReadOnlyQuery("SELECT -- DROP\n1");
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void EscapedQuote_InLiteral_Preserved()
    {
        var (isValid, error) = QueryValidator.ValidateReadOnlyQuery("SELECT 'It''s a DELETE' AS msg");
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void KeywordInIdentifier_NotBlocked()
    {
        // UpdateLog contains UPDATE as substring but is a valid identifier
        var (isValid, error) = QueryValidator.ValidateReadOnlyQuery("SELECT * FROM UpdateLog");
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void SemicolonInsideStringLiteral_Allowed()
    {
        var (isValid, error) = QueryValidator.ValidateReadOnlyQuery("SELECT ';' AS semi");
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void Subquery_Allowed()
    {
        var (isValid, error) = QueryValidator.ValidateReadOnlyQuery("SELECT * FROM (SELECT 1 AS a) AS t");
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void SelectInto_IsRejected()
    {
        var (isValid, error) = QueryValidator.ValidateReadOnlyQuery("SELECT * INTO #Temp FROM Users");
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("INTO", error, StringComparison.OrdinalIgnoreCase);
    }
}
