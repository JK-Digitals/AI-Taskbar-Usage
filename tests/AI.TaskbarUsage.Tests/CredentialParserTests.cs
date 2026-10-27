using System.Globalization;
using AiTaskbarUsage;

namespace AiTaskbarUsage.Tests;

public sealed class CredentialParserTests
{
    [Fact]
    public void ParsesClaudeCredentialFixtureWithoutRequiringFixedSchema()
    {
        var json = """{ "claudeAiOauth": { "accessToken": "oauth-test-token", "expiresAt": "2026-10-13T12:00:00Z" } }""";

        Assert.True(CredentialParser.TryParseClaude(json, out var credential));
        Assert.Equal("oauth-test-token", credential.AccessToken);
        Assert.Equal(DateTimeOffset.Parse("2026-10-13T12:00:00Z", CultureInfo.InvariantCulture), credential.ExpiresAt);
    }

    [Fact]
    public void ParsesCodexNestedTokensFixture()
    {
        var json = """{ "tokens": { "access_token": "access-test-token", "account_id": "acct-test" } }""";

        Assert.True(CredentialParser.TryParseCodex(json, out var credential));
        Assert.Equal("access-test-token", credential.AccessToken);
        Assert.Equal("acct-test", credential.AccountId);
    }

    [Fact]
    public void IgnoresMalformedAndTokenlessFiles()
    {
        Assert.False(CredentialParser.TryParseClaude("{", out _));
        Assert.False(CredentialParser.TryParseCodex("""{ "tokens": {} }""", out _));
    }
}
