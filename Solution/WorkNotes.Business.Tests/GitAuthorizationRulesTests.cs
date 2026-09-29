using WorkNotes.Business.Models;

namespace WorkNotes.Business.Tests;

public sealed class GitAuthorizationRulesTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CodeChallengeIsTheS256OfTheVerifier()
    {
        // The example of RFC 7636, appendix B.
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            GitAuthorizationRules.CodeChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void StatesAndVerifiersAreRandomUrlSafeValuesOfPkceLength()
    {
        var values = Enumerable.Range(0, 20)
            .SelectMany(_ => new[] { GitAuthorizationRules.NewState(), GitAuthorizationRules.NewCodeVerifier() })
            .ToList();

        Assert.Equal(values.Count, values.Distinct().Count());
        Assert.All(values, value =>
        {
            Assert.Equal(43, value.Length);
            Assert.Matches("^[A-Za-z0-9_-]+$", value);
        });
    }

    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "abd", false)]
    [InlineData("abc", "ABC", false)]
    [InlineData("abc", "abcd", false)]
    [InlineData("abc", "", false)]
    [InlineData("abc", null, false)]
    [InlineData(null, null, false)]
    [InlineData("", "", false)]
    public void OnlyTheSameStateMatches(string? expected, string? received, bool matches) =>
        Assert.Equal(matches, GitAuthorizationRules.StateMatches(expected, received));

    [Theory]
    [InlineData("583231", "octocat", true)]
    [InlineData("", "octocat", false)]
    [InlineData("583231", " ", false)]
    [InlineData("583231", "octo\ncat", false)]
    public void AnAccountNeedsAnIdAndALogin(string id, string login, bool valid) =>
        Assert.Equal(valid, GitAuthorizationRules.ValidAccount(new GitAccount(id, login)));

    [Fact]
    public void AnAccountLongerThanTheColumnsIsRefused()
    {
        Assert.False(GitAuthorizationRules.ValidAccount(new GitAccount("1", new string('a', GitAuthorizationRules.AccountLoginMaxLength + 1))));
        Assert.False(GitAuthorizationRules.ValidAccount(new GitAccount(new string('1', GitAuthorizationRules.AccountIdMaxLength + 1), "a")));
    }

    [Theory]
    [InlineData(" repo read:user ", "repo read:user")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void ScopesAreTrimmedOrNull(string? scopes, string? expected) =>
        Assert.Equal(expected, GitAuthorizationRules.NormalizeScopes(scopes));

    [Fact]
    public void ScopesLongerThanTheColumnAreNotStored() =>
        Assert.Null(GitAuthorizationRules.NormalizeScopes(new string('a', GitAuthorizationRules.ScopesMaxLength + 1)));

    [Fact]
    public void ATokenThatDoesNotExpireIsNeverRefreshed() =>
        Assert.False(GitAuthorizationRules.NeedsRefresh(Tokens(expires: null), Now));

    [Theory]
    [InlineData(120, false)]
    [InlineData(60, true)]
    [InlineData(30, true)]
    [InlineData(-10, true)]
    public void ATokenIsRefreshedWithinAMinuteOfItsExpiry(int secondsLeft, bool refresh) =>
        Assert.Equal(refresh, GitAuthorizationRules.NeedsRefresh(Tokens(expires: Now.AddSeconds(secondsLeft)), Now));

    [Fact]
    public void OnlyAValidRefreshTokenCanRefresh()
    {
        Assert.True(GitAuthorizationRules.CanRefresh(Tokens(refresh: "r", refreshExpires: Now.AddDays(1)), Now));
        Assert.True(GitAuthorizationRules.CanRefresh(Tokens(refresh: "r", refreshExpires: null), Now));
        Assert.False(GitAuthorizationRules.CanRefresh(Tokens(refresh: "r", refreshExpires: Now), Now));
        Assert.False(GitAuthorizationRules.CanRefresh(Tokens(refresh: null), Now));
    }

    [Fact]
    public void TokensAreLeftOutOfTheirText()
    {
        var text = Tokens(refresh: "refresh-secret") with { AccessToken = "access-secret" };

        Assert.DoesNotContain("access-secret", text.ToString());
        Assert.DoesNotContain("refresh-secret", text.ToString());
        Assert.DoesNotContain("access-secret", new GitCredential("1", "octocat", text, Now, Now).ToString());
    }

    private static GitTokens Tokens(DateTime? expires = null, string? refresh = null, DateTime? refreshExpires = null) =>
        new("access", refresh, expires, refreshExpires, null);
}
