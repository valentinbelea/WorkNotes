using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Business.Services;

namespace WorkNotes.Business.Tests;

public sealed class GitRepositoryServiceTests
{
    private const string UserId = "user-1";
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 29, 12, 0, 0, 700, TimeSpan.Zero);
    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly GitCredential Credential =
        new("583231", "octocat", new GitTokens("access-1", null, null, null, null), Now, Now);

    [Fact]
    public async Task EveryListedRepositoryIsShownWithItsImportedChoiceInNameOrder()
    {
        var gitHub = new StubGitHub(Repo("3", "zeta/app"), Repo("1", "Alpha/api"), Repo("2", "beta/web"));
        var repositories = new StubRepositories(Imported(Repo("2", "beta/web")));

        var selection = await Service(gitHub, repositories).GetSelectionAsync(UserId, CancellationToken.None);

        Assert.Equal(GitRepositoryStatus.Succeeded, selection.Status);
        Assert.Equal(["Alpha/api", "beta/web", "zeta/app"], selection.Repositories.Select(choice => choice.Repository.FullName));
        Assert.Equal([false, true, false], selection.Repositories.Select(choice => choice.IsImported));
        Assert.All(selection.Repositories, choice => Assert.True(choice.IsAccessible));
        Assert.Equal("access-1", gitHub.ReceivedAccessToken);
    }

    [Fact]
    public async Task AnImportedRepositoryGitHubNoLongerListsIsShownAsNotAccessible()
    {
        var gitHub = new StubGitHub(Repo("1", "octocat/api"));
        var repositories = new StubRepositories(Imported(Repo("9", "octocat/gone")));

        var selection = await Service(gitHub, repositories).GetSelectionAsync(UserId, CancellationToken.None);

        var gone = Assert.Single(selection.Repositories, choice => choice.Repository.Id == "9");
        Assert.True(gone.IsImported);
        Assert.False(gone.IsAccessible);
    }

    [Fact]
    public async Task ListedRepositoriesAreNormalizedAndInvalidOnesLeftOut()
    {
        var gitHub = new StubGitHub(Repo("1", " octocat/api "), Repo("x", "octocat/bad"), Repo("2", "octocat/web") with { HtmlUrl = "ftp://x" },
            Repo("1", "octocat/api-duplicate"));

        var selection = await Service(gitHub, new StubRepositories()).GetSelectionAsync(UserId, CancellationToken.None);

        Assert.Equal("octocat/api", Assert.Single(selection.Repositories).Repository.FullName);
    }

    [Fact]
    public async Task ATruncatedListIsReported()
    {
        var gitHub = new StubGitHub(Repo("1", "octocat/api")) { Truncated = true };

        Assert.True((await Service(gitHub, new StubRepositories()).GetSelectionAsync(UserId, CancellationToken.None)).Truncated);
    }

    [Theory]
    [InlineData(GitVerifyStatus.NotConfigured, GitRepositoryStatus.NotConfigured)]
    [InlineData(GitVerifyStatus.NotConnected, GitRepositoryStatus.NotConnected)]
    [InlineData(GitVerifyStatus.ReconnectRequired, GitRepositoryStatus.ReconnectRequired)]
    [InlineData(GitVerifyStatus.Unavailable, GitRepositoryStatus.Unavailable)]
    public async Task WithoutAUsableTokenOnlyTheImportedRepositoriesAreShown(GitVerifyStatus token, GitRepositoryStatus expected)
    {
        var gitHub = new StubGitHub(Repo("1", "octocat/api"));
        var repositories = new StubRepositories(Imported(Repo("2", "octocat/web")));

        var selection = await Service(gitHub, repositories, GitAccessToken.Failed(token)).GetSelectionAsync(UserId, CancellationToken.None);

        Assert.Equal(expected, selection.Status);
        Assert.Equal("octocat/web", Assert.Single(selection.Repositories).Repository.FullName);
        Assert.Null(gitHub.ReceivedAccessToken);
    }

    [Theory]
    [InlineData(GitProviderStatus.Rejected, GitRepositoryStatus.ReconnectRequired)]
    [InlineData(GitProviderStatus.Unavailable, GitRepositoryStatus.Unavailable)]
    public async Task AListGitHubDoesNotGiveShowsOnlyTheImportedRepositories(GitProviderStatus listing, GitRepositoryStatus expected)
    {
        var gitHub = new StubGitHub { Listing = listing };
        var repositories = new StubRepositories(Imported(Repo("2", "octocat/web")));

        var selection = await Service(gitHub, repositories).GetSelectionAsync(UserId, CancellationToken.None);

        Assert.Equal(expected, selection.Status);
        Assert.Single(selection.Repositories);
    }

    [Fact]
    public async Task SavingStoresTheSelectedRepositoriesAsGitHubDescribesThem()
    {
        var gitHub = new StubGitHub(Repo("1", "octocat/api") with { Description = "New" }, Repo("2", "octocat/web"), Repo("3", "octocat/docs"));
        var repositories = new StubRepositories(Imported(Repo("1", "octocat/old-name")), Imported(Repo("3", "octocat/docs")));

        var status = await Service(gitHub, repositories).SaveSelectionAsync(UserId, ["1", "2"], CancellationToken.None);

        Assert.Equal(GitRepositoryStatus.Succeeded, status);
        var saved = Assert.Single(repositories.Replacements);
        Assert.Equal((UserId, GitProviders.GitHub, Now), (saved.UserId, saved.Provider, saved.NowUtc));
        Assert.Equal([Repo("1", "octocat/api") with { Description = "New" }, Repo("2", "octocat/web")], saved.Repositories);
    }

    [Fact]
    public async Task AnImportedRepositoryGitHubNoLongerListsIsKeptOnlyWhileSelected()
    {
        var gitHub = new StubGitHub(Repo("1", "octocat/api"));
        var gone = Repo("9", "octocat/gone");

        var kept = new StubRepositories(Imported(gone));
        await Service(gitHub, kept).SaveSelectionAsync(UserId, ["9"], CancellationToken.None);
        var removed = new StubRepositories(Imported(gone));
        await Service(gitHub, removed).SaveSelectionAsync(UserId, [], CancellationToken.None);

        Assert.Equal([gone], Assert.Single(kept.Replacements).Repositories);
        Assert.Empty(Assert.Single(removed.Replacements).Repositories);
    }

    [Fact]
    public async Task UnknownInvalidAndRepeatedIdsAreIgnored()
    {
        var gitHub = new StubGitHub(Repo("1", "octocat/api"));
        var repositories = new StubRepositories();

        await Service(gitHub, repositories).SaveSelectionAsync(UserId, ["1", "1", "404", "abc", "", "1; DROP TABLE"], CancellationToken.None);

        Assert.Equal([Repo("1", "octocat/api")], Assert.Single(repositories.Replacements).Repositories);
    }

    [Theory]
    [InlineData(GitVerifyStatus.NotConnected, GitRepositoryStatus.NotConnected)]
    [InlineData(GitVerifyStatus.Unavailable, GitRepositoryStatus.Unavailable)]
    public async Task NothingIsSavedWithoutAUsableToken(GitVerifyStatus token, GitRepositoryStatus expected)
    {
        var repositories = new StubRepositories(Imported(Repo("1", "octocat/api")));

        var status = await Service(new StubGitHub(), repositories, GitAccessToken.Failed(token))
            .SaveSelectionAsync(UserId, [], CancellationToken.None);

        Assert.Equal(expected, status);
        Assert.Empty(repositories.Replacements);
    }

    [Fact]
    public async Task NothingIsSavedWhenGitHubDoesNotList()
    {
        var repositories = new StubRepositories(Imported(Repo("1", "octocat/api")));

        var status = await Service(new StubGitHub { Listing = GitProviderStatus.Unavailable }, repositories)
            .SaveSelectionAsync(UserId, [], CancellationToken.None);

        Assert.Equal(GitRepositoryStatus.Unavailable, status);
        Assert.Empty(repositories.Replacements);
    }

    [Fact]
    public async Task AConcurrentSaveIsAConflict()
    {
        var repositories = new StubRepositories { ReplaceSucceeds = false };

        Assert.Equal(GitRepositoryStatus.Conflict,
            await Service(new StubGitHub(Repo("1", "octocat/api")), repositories).SaveSelectionAsync(UserId, ["1"], CancellationToken.None));
    }

    [Fact]
    public async Task CancelledRequestsStopBeforeDataAccess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repositories = new StubRepositories();
        var service = Service(new StubGitHub(), repositories);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetSelectionAsync(UserId, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveSelectionAsync(UserId, [], cancellation.Token));
        Assert.Equal(0, repositories.Calls);
    }

    [Fact]
    public async Task TheRequestTokenReachesEveryDependency()
    {
        using var cancellation = new CancellationTokenSource();
        var gitHub = new StubGitHub(Repo("1", "octocat/api"));
        var repositories = new StubRepositories();
        var tokens = new StubTokens(new GitAccessToken(GitVerifyStatus.Valid, Credential));

        await new GitRepositoryService(tokens, gitHub, repositories, new FixedTime(UtcNow)).SaveSelectionAsync(UserId, ["1"], cancellation.Token);

        Assert.Equal(cancellation.Token, tokens.ReceivedToken);
        Assert.Equal(cancellation.Token, gitHub.ReceivedToken);
        Assert.All(repositories.ReceivedTokens, token => Assert.Equal(cancellation.Token, token));
        Assert.Equal(2, repositories.ReceivedTokens.Count);
    }

    [Fact]
    public async Task ARepositoryFailureIsNotAnEmptyList()
    {
        var expected = new InvalidOperationException("Database unavailable.");
        var service = Service(new StubGitHub(), new StubRepositories { Error = expected });

        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetSelectionAsync(UserId, CancellationToken.None)));
    }

    private static GitRepositoryService Service(StubGitHub gitHub, StubRepositories repositories, GitAccessToken? token = null) =>
        new(new StubTokens(token ?? new GitAccessToken(GitVerifyStatus.Valid, Credential)), gitHub, repositories, new FixedTime(UtcNow));

    private static GitRepositoryInfo Repo(string id, string fullName) =>
        new(id, fullName, null, false, "main", $"https://github.com/{fullName.Trim()}");

    private static GitRepository Imported(GitRepositoryInfo info) => new(info, Now.AddDays(-5), Now.AddDays(-1));

    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubTokens(GitAccessToken token) : IGitHubTokenService
    {
        public CancellationToken ReceivedToken { get; private set; }

        public Task<GitAccessToken> GetAsync(string userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedToken = cancellationToken;
            return Task.FromResult(token);
        }
    }

    private sealed class StubGitHub(params GitRepositoryInfo[] listed) : IGitHubOAuthClient
    {
        public GitProviderStatus Listing { get; init; } = GitProviderStatus.Succeeded;
        public bool Truncated { get; init; }
        public string? ReceivedAccessToken { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public bool IsConfigured => true;

        public Task<GitProviderResult<GitRepositoryCatalog>> GetRepositoriesAsync(string accessToken, CancellationToken cancellationToken)
        {
            (ReceivedAccessToken, ReceivedToken) = (accessToken, cancellationToken);
            return Task.FromResult(Listing switch
            {
                GitProviderStatus.Succeeded => GitProviderResult<GitRepositoryCatalog>.Succeeded(new GitRepositoryCatalog(listed, Truncated)),
                GitProviderStatus.Rejected => GitProviderResult<GitRepositoryCatalog>.Rejected,
                _ => GitProviderResult<GitRepositoryCatalog>.Unavailable
            });
        }

        public string GetAuthorizationUrl(string state, string codeChallenge, string redirectUri) => throw new NotSupportedException();
        public Task<GitProviderResult<GitTokens>> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<GitProviderResult<GitTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<GitProviderResult<GitAccount>> GetAccountAsync(string accessToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<GitProviderStatus> RevokeAsync(string accessToken, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubRepositories(params GitRepository[] imported) : IGitRepositoryRepository
    {
        public bool ReplaceSucceeds { get; init; } = true;
        public Exception? Error { get; init; }
        public int Calls { get; private set; }
        public List<CancellationToken> ReceivedTokens { get; } = [];
        public List<(string UserId, string Provider, IReadOnlyList<GitRepositoryInfo> Repositories, DateTime NowUtc)> Replacements { get; } = [];

        public Task<IReadOnlyList<GitRepository>> GetImportedAsync(string userId, string provider, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            return Task.FromResult<IReadOnlyList<GitRepository>>(imported);
        }

        public Task<bool> ReplaceAsync(string userId, string provider, IReadOnlyList<GitRepositoryInfo> repositories, DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            if (ReplaceSucceeds) Replacements.Add((userId, provider, repositories, nowUtc));
            return Task.FromResult(ReplaceSucceeds);
        }

        private void Record(CancellationToken cancellationToken)
        {
            Calls++;
            ReceivedTokens.Add(cancellationToken);
            if (Error is not null) throw Error;
        }
    }
}
