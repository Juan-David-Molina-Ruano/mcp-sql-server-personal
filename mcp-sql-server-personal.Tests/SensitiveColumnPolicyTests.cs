using McpSqlServerPersonal.Security;
using Xunit;

namespace McpSqlServerPersonal.Tests;

public class SensitiveColumnPolicyTests
{
    private readonly SensitiveColumnPolicy _policy = new();

    [Theory]
    [InlineData("password")]
    [InlineData("user_password")]
    [InlineData("PasswordHash")]
    [InlineData("pwd")]
    [InlineData("passwd")]
    [InlineData("hash")]
    [InlineData("salt")]
    [InlineData("token")]
    [InlineData("secret")]
    [InlineData("ssn")]
    [InlineData("SocialSecurityNumber")]
    [InlineData("credit_card")]
    [InlineData("CreditCard")]
    [InlineData("cvv")]
    [InlineData("cvc")]
    [InlineData("iban")]
    [InlineData("routing_number")]
    [InlineData("RoutingNumber")]
    [InlineData("private_key")]
    [InlineData("PrivateKey")]
    public void StrongPatterns_WithoutLineage_AreSensitive(string columnName)
    {
        Assert.True(_policy.IsSensitive(columnName));
    }

    [Theory]
    [InlineData("api_key")]
    [InlineData("apikey")]
    [InlineData("pin")]
    public void BroadPatterns_WithoutLineage_AreNotSensitive(string columnName)
    {
        Assert.False(_policy.IsSensitive(columnName));
    }

    [Theory]
    [InlineData("api_key", "api_key")]
    [InlineData("apikey", "apikey")]
    [InlineData("MyApiKey", "MyApiKey")]
    [InlineData("pin", "pin")]
    public void BroadPatterns_WithLineage_AreSensitive(string columnName, string baseColumnName)
    {
        Assert.True(_policy.IsSensitive(columnName, baseColumnName));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("id")]
    [InlineData("created_at")]
    [InlineData("description")]
    [InlineData("is_active")]
    [InlineData("email")] // email is not in the strong list intentionally
    [InlineData("ApiKeyName")] // broad pattern substring without lineage should not trigger
    public void NonSensitiveColumns_AreNotSensitive(string columnName)
    {
        Assert.False(_policy.IsSensitive(columnName));
    }

    [Fact]
    public void IsSensitive_WithReason_ReturnsReason()
    {
        bool result = _policy.IsSensitive("password", null, out string? reason);
        Assert.True(result);
        Assert.NotNull(reason);
        Assert.Contains("password", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CustomPatterns_OverrideDefaults()
    {
        var custom = new SensitiveColumnPolicy(new[] { "custom_secret" }, Array.Empty<string>());
        Assert.True(custom.IsSensitive("my_custom_secret"));
        Assert.False(custom.IsSensitive("password")); // default strong pattern removed
    }

    [Fact]
    public void LegacyConstructor_TreatsAllAsStrong()
    {
        var legacy = new SensitiveColumnPolicy(new[] { "legacy_pattern" });
        Assert.True(legacy.IsSensitive("legacy_pattern"));
        Assert.True(legacy.IsSensitive("my_legacy_pattern"));
    }
}
