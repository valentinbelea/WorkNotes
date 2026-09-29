using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Business.Services;

namespace WorkNotes.Business.Tests;

public sealed class GitHubConnectionServiceTests
{
    private const string UserId = "user-1";
    private const string Callback = "https://localhost:7190/Account/GitHub/Callback";
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 29, 12, 0, 0, 500, TimeSpan.Zero);
    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly GitHubPendingAuthorization Pending = new("state-1", "verifier-1");
    private static readonly GitTokens Issued = new("access-2", null, null, null, " repo ");

    [Fact]
    public void AuthorizationSendsTheStateAndTheChallengeOfItsVerifier()
    {
        var gitHub = new StubGitHub();
        var service = Service(gitHub, new StubConnections());

        var authorization = service.StartAuthorization(Callback);

        Assert.Equal(gitHub.AuthorizationUrl, authorization.Url);
        Assert.Equal(authorization.Pending.State, gitHub.ReceivedState);
        Assert.Equal(GitAuthorizationRules.CodeChallenge(authorization.Pending.CodeVerifier), gitHub.ReceivedChallenge);
        Assert.Equal(Callback, gitHub.ReceivedRedirectUri);
        Assert.NotEqual(authorization.Pending.State, service.StartAuthorization(Callback).Pending.State);
    }

    [Fact]
    public void AuthorizationCannotStartWithoutConfiguration() =>
        Assert.Throws<InvalidOperationException>(
            () => Service(new StubGitHub { IsConfigured = false }, new StubConnections()).StartAuthorization(Callback));

    [Fact]
    public async Task ACallbackIsExchangedWithTheVerifierAndSavesTheAccount()
    {
        var gitHub = new StubGitHub();
        var connections = new StubConnections();

        var status = await Service(gitHub, connections).CompleteAuthorizationAsync(UserId, Pending,
            new GitHubCallback("code-1", "state-1", null), Callback, CancellationToken.None);

        Assert.Equal(GitConnectStatus.Connected, status);
        Assert.Equal(("code-1", "verifier-1", Callback), gitHub.ReceivedExchange);
        Assert.Equal("access-2", gitHub.ReceivedAccessToken);
        var saved = Assert.Single(connections.Saves);
        Assert.Equal((UserId, GitProviders.GitHub), (saved.UserId, saved.Provider));
        Assert.Equal(new GitCredential("583231", "octocat", Issued with { Scopes = "repo" }, Now, Now), saved.Credential);
    }

    [Theory]
    [InlineData("state-2")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ACallbackWithAnotherStateIsRefusedBeforeGitHubIsCalled(string? state)
    {
        var gitHub = new StubGitHub();
        var connections = new StubConnections();

        var status = await Service(gitHub, connections).CompleteAuthorizationAsync(UserId, Pending,
            new GitHubCallback("code-1", state, null), Callback, CancellationToken.None);

        Assert.Equal(GitConnectStatus.InvalidState, status);
        Assert.Null(gitHub.ReceivedExchange);
        Assert.Empty(connections.Saves);
    }

    [Fact]
    public async Task ACallbackWithoutAPendingAuthorizationIsRefused()
    {
        var gitHub = new StubGitHub();

        var status = await Service(gitHub, new StubConnections()).CompleteAuthorizationAsync(UserId, null,
            new GitHubCallback("code-1", "state-1", null), Callback, CancellationToken.None);

        Assert.Equal(GitConnectStatus.InvalidState, status);
        Assert.Null(gitHub.ReceivedExchange);
    }

    [Fact]
    public async Task AnErrorWithAnotherStateIsAnInvalidState()
    {
        var status = await Service(new StubGitHub(), new StubConnections()).CompleteAuthorizationAsync(UserId, Pending,
            new GitHubCallback(null, "state-2", "access_denied"), Callback, CancellationToken.None);

        Assert.Equal(GitConnectStatus.InvalidState, status);
    }

    [Theory]
    [InlineData("access_denied", GitConnectStatus.Denied)]
    [InlineData("redirect_uri_mismatch", GitConnectStatus.Rejected)]
    public async Task AnAuthorizationRefusedOnGitHubSavesNothing(string error, GitConnectStatus expected)
    {
        var gitHub = new StubGitHub();
        var connections = new StubConnections();

        var status = await Service(gitHub, connections).CompleteAuthorizationAsync(UserId, Pending,
            new GitHubCallback(null, "state-1", error), Callback, CancellationToken.None);

        Assert.Equal(expected, status);
        Assert.Null(gitHub.ReceivedExchange);
        Assert.Empty(connections.Saves);
    }

    [Fact]
    public async Task ACallbackWithoutACodeIsRejected()
    {
        var status = await Service(new StubGitHub(), new StubConnections()).CompleteAuthorizationAsync(UserId, Pending,
            new GitHubCallback(" ", "state-1", null), Callback, CancellationToken.None);

        Assert.Equal(GitConnectStatus.Rejected, status);
    }

    [Theory]
    [InlineData(GitProviderStatus.Rejected, GitConnectStatus.Rejected)]
    [InlineData(GitProviderStatus.Unavailable, GitConnectStatus.Unavailable)]
    public async Task ACodeGitHubDoesNotExchangeSavesNothing(GitProviderStatus exchange, GitConnectStatus expected)
    {
        var gitHub = new StubGitHub { Exchange = Result<GitTokens>(exchange) };
        var connections = new StubConnections();

        var status = await Service(gitHub, connections).CompleteAuthorizationAsync(UserId, Pending,
            new GitHubCallback("code-1", "state-1", null), Callback, CancellationToken.None);

        Assert.Equal(expected, status);
        Assert.Null(gitHub.ReceivedAccessToken);
        Assert.Empty(connections.Saves);
    }

    [Theory]
    [InlineData(GitProviderStatus.Rejected, GitConnectStatus.Rejected)]
    [InlineData(GitProviderStatus.Unavailable, GitConnectStatus.Unavailable)]
    public async Task AnAccountGitHubDoesNotReturnSavesNothing(GitProviderStatus accountStatus, GitConnectStatus expected)
    {
        var connections = new StubConnections();
        var gitHub = new StubGitHub { Account = Result<GitAccount>(accountStatus) };

        var status = await Service(gitHub, connections).CompleteAuthorizationAsync(UserId, Pending,
            new GitHubCallback("code-1", "state-1", null), Callback, CancellationToken.None);

        Assert.Equal(expected, status);
        Assert.Empty(connections.Saves);
    }

    [Fact]
    public async Task AnAccountThatDoesNotFitIsRejected()
    {
        var connections = new StubConnections();
        var gitHub = new StubGitHub { Account = GitProviderResult<GitAccount>.Succeeded(new GitAccount("1", new string('a', 101))) };

        var status = await Service(gitHub, connections).CompleteAuthorizationAsync(UserId, Pending,
            new GitHubCallback("code-1", "state-1", null), Callback, CancellationToken.None);

        Assert.Equal(GitConnectStatus.Rejected, status);
        Assert.Empty(connections.Saves);
    }

    [Fact]
    public async Task ACallbackWithoutConfigurationIsNotConfigured()
    {
        var gitHub = new StubGitHub { IsConfigured = false };

        var status = await Service(gitHub, new StubConnections()).CompleteAuthorizationAsync(UserId, Pending,
            new GitHubCallback("code-1", "state-1", null), Callback, CancellationToken.None);

        Assert.Equal(GitConnectStatus.NotConfigured, status);
        Assert.Null(gitHub.ReceivedExchange);
    }

    [Fact]
    public async Task AValidTokenIsVerifiedAndTheRenamedLoginSaved()
    {
        var stored = Stored(new GitTokens("access-1", null, null, null, null));
        var connections = new StubConnections(stored);
        var gitHub = new StubGitHub { Account = GitProviderResult<GitAccount>.Succeeded(new GitAccount("583231", "octocat-renamed")) };

        var status = await Service(gitHub, connections).VerifyAsync(UserId, CancellationToken.None);

        Assert.Equal(GitVerifyStatus.Valid, status);
        Assert.Equal("access-1", gitHub.ReceivedAccessToken);
        Assert.Null(gitHub.ReceivedRefreshToken);
        var update = Assert.Single(connections.Updates);
        Assert.Equal(stored with { AccountLogin = "octocat-renamed", ValidatedAtUtc = Now }, update);
    }

    [Fact]
    public async Task AnExpiringTokenIsRefreshedAndSavedBeforeItIsVerified()
    {
        var connections = new StubConnections(Stored(new GitTokens("access-1", "refresh-1", Now.AddSeconds(30), Now.AddDays(90), null)));
        var fresh = new GitTokens("access-2", "refresh-2", Now.AddHours(8), Now.AddDays(180), null);
        var gitHub = new StubGitHub { Refresh = GitProviderResult<GitTokens>.Succeeded(fresh), Account = Result<GitAccount>(GitProviderStatus.Unavailable) };

        var status = await Service(gitHub, connections).VerifyAsync(UserId, CancellationToken.None);

        // GitHub did not answer the verification, but the new tokens are kept: the old refresh token is spent.
        Assert.Equal(GitVerifyStatus.Unavailable, status);
        Assert.Equal("refresh-1", gitHub.ReceivedRefreshToken);
        Assert.Equal("access-2", gitHub.ReceivedAccessToken);
        Assert.Equal(fresh, Assert.Single(connections.Updates).Tokens);
    }

    [Fact]
    public async Task AnExpiredTokenWithoutARefreshTokenNeedsANewConnection()
    {
        var gitHub = new StubGitHub();
        var connections = new StubConnections(Stored(new GitTokens("access-1", "refresh-1", Now.AddHours(-1), Now.AddHours(-1), null)));

        var status = await Service(gitHub, connections).VerifyAsync(UserId, CancellationToken.None);

        Assert.Equal(GitVerifyStatus.ReconnectRequired, status);
        Assert.Null(gitHub.ReceivedRefreshToken);
        Assert.Empty(connections.Updates);
    }

    [Fact]
    public async Task ARefusedRefreshNeedsANewConnection()
    {
        var connections = new StubConnections(Stored(new GitTokens("access-1", "refresh-1", Now, null, null)));
        var gitHub = new StubGitHub { Refresh = GitProviderResult<GitTokens>.Rejected };

        var status = await Service(gitHub, connections).VerifyAsync(UserId, CancellationToken.None);

        Assert.Equal(GitVerifyStatus.ReconnectRequired, status);
        Assert.Empty(connections.Updates);
    }

    [Fact]
    public async Task ARefreshDoneMeanwhileByAnotherRequestIsUsed()
    {
        var connections = new StubConnections(Stored(new GitTokens("access-1", "refresh-1", Now, null, null)));
        var concurrent = Stored(new GitTokens("access-3", "refresh-3", Now.AddHours(8), null, null));
        connections.AfterFirstRead = concurrent;
        var gitHub = new StubGitHub { Refresh = GitProviderResult<GitTokens>.Rejected };

        var status = await Service(gitHub, connections).VerifyAsync(UserId, CancellationToken.None);

        Assert.Equal(GitVerifyStatus.Valid, status);
        Assert.Equal("access-3", gitHub.ReceivedAccessToken);
        Assert.Equal(concurrent.Tokens, Assert.Single(connections.Updates).Tokens);
    }

    [Fact]
    public async Task AnUnavailableRefreshIsNotAReconnection()
    {
        var connections = new StubConnections(Stored(new GitTokens("access-1", "refresh-1", Now, null, null)));
        var gitHub = new StubGitHub { Refresh = GitProviderResult<GitTokens>.Unavailable };

        Assert.Equal(GitVerifyStatus.Unavailable, await Service(gitHub, connections).VerifyAsync(UserId, CancellationToken.None));
    }

    [Theory]
    [InlineData(GitProviderStatus.Rejected, GitVerifyStatus.ReconnectRequired)]
    [InlineData(GitProviderStatus.Unavailable, GitVerifyStatus.Unavailable)]
    public async Task ATokenGitHubDoesNotAcceptIsReported(GitProviderStatus accountStatus, GitVerifyStatus expected)
    {
        var connections = new StubConnections(Stored(new GitTokens("access-1", null, null, null, null)));
        var gitHub = new StubGitHub { Account = Result<GitAccount>(accountStatus) };

        Assert.Equal(expected, await Service(gitHub, connections).VerifyAsync(UserId, CancellationToken.None));
        Assert.Empty(connections.Updates);
    }

    [Fact]
    public async Task TokensThatCannotBeDecryptedNeedANewConnection()
    {
        var gitHub = new StubGitHub();

        var status = await Service(gitHub, new StubConnections(Stored(null))).VerifyAsync(UserId, CancellationToken.None);

        Assert.Equal(GitVerifyStatus.ReconnectRequired, status);
        Assert.Null(gitHub.ReceivedAccessToken);
    }

    [Fact]
    public async Task VerifyingWithoutAConnectionIsNotConnected() =>
        Assert.Equal(GitVerifyStatus.NotConnected,
            await Service(new StubGitHub(), new StubConnections()).VerifyAsync(UserId, CancellationToken.None));

    [Fact]
    public async Task AConnectionDeletedDuringTheVerificationIsNotConnected()
    {
        var connections = new StubConnections(Stored(new GitTokens("access-1", null, null, null, null))) { UpdateSucceeds = false };

        Assert.Equal(GitVerifyStatus.NotConnected,
            await Service(new StubGitHub(), connections).VerifyAsync(UserId, CancellationToken.None));
    }

    [Fact]
    public async Task DisconnectingDeletesTheConnectionAndRevokesItsAuthorization()
    {
        var connections = new StubConnections(Stored(new GitTokens("access-1", null, null, null, null)));
        var gitHub = new StubGitHub();

        var status = await Service(gitHub, connections).DisconnectAsync(UserId, CancellationToken.None);

        Assert.Equal(GitDisconnectStatus.Disconnected, status);
        Assert.True(connections.Deleted);
        Assert.Equal("access-1", gitHub.RevokedToken);
    }

    [Theory]
    [InlineData(GitProviderStatus.Rejected)]
    [InlineData(GitProviderStatus.Unavailable)]
    public async Task AConnectionIsDeletedEvenWhenGitHubCannotRevokeIt(GitProviderStatus revoke)
    {
        var connections = new StubConnections(Stored(new GitTokens("access-1", null, null, null, null)));

        var status = await Service(new StubGitHub { Revoke = revoke }, connections).DisconnectAsync(UserId, CancellationToken.None);

        Assert.Equal(GitDisconnectStatus.NotRevoked, status);
        Assert.True(connections.Deleted);
    }

    [Fact]
    public async Task TokensThatCannotBeDecryptedAreDeletedWithoutRevocation()
    {
        var gitHub = new StubGitHub();
        var connections = new StubConnections(Stored(null));

        var status = await Service(gitHub, connections).DisconnectAsync(UserId, CancellationToken.None);

        Assert.Equal(GitDisconnectStatus.NotRevoked, status);
        Assert.True(connections.Deleted);
        Assert.Null(gitHub.RevokedToken);
    }

    [Fact]
    public async Task DisconnectingWithoutAConnectionIsNotConnected()
    {
        var gitHub = new StubGitHub();

        Assert.Equal(GitDisconnectStatus.NotConnected,
            await Service(gitHub, new StubConnections()).DisconnectAsync(UserId, CancellationToken.None));
        Assert.Null(gitHub.RevokedToken);
    }

    [Fact]
    public async Task TheConnectionIsReadForTheUserWithTheirToken()
    {
        using var cancellation = new CancellationTokenSource();
        var connections = new StubConnections(Stored(new GitTokens("access-1", null, null, null, null)));

        var connection = await Service(new StubGitHub(), connections).GetAsync(UserId, cancellation.Token);

        Assert.Equal("octocat", connection?.AccountLogin);
        Assert.Equal((UserId, GitProviders.GitHub, cancellation.Token), connections.ReceivedRead);
    }

    [Fact]
    public async Task CancelledRequestsStopBeforeDataAccess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var gitHub = new StubGitHub();
        var connections = new StubConnections(Stored(new GitTokens("access-1", null, null, null, null)));
        var service = Service(gitHub, connections);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(UserId, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CompleteAuthorizationAsync(UserId, Pending,
            new GitHubCallback("code-1", "state-1", null), Callback, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.VerifyAsync(UserId, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DisconnectAsync(UserId, cancellation.Token));
        Assert.Equal(0, connections.Calls);
        Assert.Null(gitHub.ReceivedExchange);
    }

    [Fact]
    public async Task TheRequestTokenReachesGitHubAndTheRepository()
    {
        using var cancellation = new CancellationTokenSource();
        var gitHub = new StubGitHub();
        var connections = new StubConnections();

        await Service(gitHub, connections).CompleteAuthorizationAsync(UserId, Pending,
            new GitHubCallback("code-1", "state-1", null), Callback, cancellation.Token);

        Assert.All(gitHub.ReceivedTokens.Concat(connections.ReceivedTokens), token => Assert.Equal(cancellation.Token, token));
        Assert.NotEmpty(gitHub.ReceivedTokens);
        Assert.NotEmpty(connections.ReceivedTokens);
    }

    [Fact]
    public async Task ARepositoryFailureIsNotReportedAsAMissingConnection()
    {
        var expected = new InvalidOperationException("Database unavailable.");
        var service = Service(new StubGitHub(), new StubConnections { Error = expected });

        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(UserId, CancellationToken.None)));
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync(UserId, CancellationToken.None)));
    }

    private static GitHubConnectionService Service(StubGitHub gitHub, StubConnections connections)
    {
        var time = new FixedTime(UtcNow);
        return new(gitHub, connections, new GitHubTokenService(gitHub, connections, time), time);
    }

    private static GitCredential Stored(GitTokens? tokens) =>
        new("583231", "octocat", tokens, Now.AddDays(-10), Now.AddDays(-1));

    private static GitProviderResult<T> Result<T>(GitProviderStatus status) where T : class =>
        status == GitProviderStatus.Rejected ? GitProviderResult<T>.Rejected : GitProviderResult<T>.Unavailable;

    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubGitHub : IGitHubOAuthClient
    {
        public bool IsConfigured { get; init; } = true;
        public string AuthorizationUrl { get; } = "https://github.com/login/oauth/authorize?test";
        public GitProviderResult<GitTokens> Exchange { get; init; } = GitProviderResult<GitTokens>.Succeeded(Issued);
        public GitProviderResult<GitTokens> Refresh { get; init; } = GitProviderResult<GitTokens>.Rejected;
        public GitProviderResult<GitAccount> Account { get; init; } =
            GitProviderResult<GitAccount>.Succeeded(new GitAccount("583231", "octocat"));
        public GitProviderStatus Revoke { get; init; } = GitProviderStatus.Succeeded;

        public string? ReceivedState { get; private set; }
        public string? ReceivedChallenge { get; private set; }
        public string? ReceivedRedirectUri { get; private set; }
        public (string Code, string Verifier, string RedirectUri)? ReceivedExchange { get; private set; }
        public string? ReceivedRefreshToken { get; private set; }
        public string? ReceivedAccessToken { get; private set; }
        public string? RevokedToken { get; private set; }
        public List<CancellationToken> ReceivedTokens { get; } = [];

        public string GetAuthorizationUrl(string state, string codeChallenge, string redirectUri)
        {
            (ReceivedState, ReceivedChallenge, ReceivedRedirectUri) = (state, codeChallenge, redirectUri);
            return AuthorizationUrl;
        }

        public Task<GitProviderResult<GitTokens>> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri,
            CancellationToken cancellationToken)
        {
            ReceivedTokens.Add(cancellationToken);
            ReceivedExchange = (code, codeVerifier, redirectUri);
            return Task.FromResult(Exchange);
        }

        public Task<GitProviderResult<GitTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            ReceivedTokens.Add(cancellationToken);
            ReceivedRefreshToken = refreshToken;
            return Task.FromResult(Refresh);
        }

        public Task<GitProviderResult<GitAccount>> GetAccountAsync(string accessToken, CancellationToken cancellationToken)
        {
            ReceivedTokens.Add(cancellationToken);
            ReceivedAccessToken = accessToken;
            return Task.FromResult(Account);
        }

        public Task<GitProviderResult<GitRepositoryCatalog>> GetRepositoriesAsync(string accessToken,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The connection does not list repositories.");

        public Task<GitProviderStatus> RevokeAsync(string accessToken, CancellationToken cancellationToken)
        {
            ReceivedTokens.Add(cancellationToken);
            RevokedToken = accessToken;
            return Task.FromResult(Revoke);
        }
    }

    // One user's connection in memory; AfterFirstRead is what a concurrent request leaves for the next read.
    private sealed class StubConnections(GitCredential? stored = null) : IGitConnectionRepository
    {
        private GitCredential? current = stored;
        private int reads;

        public GitCredential? AfterFirstRead { get; set; }
        public bool UpdateSucceeds { get; init; } = true;
        public Exception? Error { get; init; }
        public int Calls { get; private set; }
        public bool Deleted { get; private set; }
        public (string UserId, string Provider, CancellationToken Token)? ReceivedRead { get; private set; }
        public List<(string UserId, string Provider, GitCredential Credential)> Saves { get; } = [];
        public List<GitCredential> Updates { get; } = [];
        public List<CancellationToken> ReceivedTokens { get; } = [];

        public Task<GitConnection?> GetAsync(string userId, string provider, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            ReceivedRead = (userId, provider, cancellationToken);
            return Task.FromResult(current is null ? null
                : new GitConnection(provider, current.AccountLogin, current.ConnectedAtUtc, current.ValidatedAtUtc,
                    current.Tokens?.AccessTokenExpiresAtUtc));
        }

        public Task<GitCredential?> GetCredentialAsync(string userId, string provider, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            if (reads++ == 1 && AfterFirstRead is not null) current = AfterFirstRead;
            return Task.FromResult(current);
        }

        public Task SaveAsync(string userId, string provider, GitCredential credential, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            Saves.Add((userId, provider, credential));
            current = credential;
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(string userId, string provider, GitCredential credential, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            if (!UpdateSucceeds || current is null) return Task.FromResult(false);
            Updates.Add(credential);
            current = credential;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(string userId, string provider, CancellationToken cancellationToken)
        {
            Record(cancellationToken);
            Deleted = current is not null;
            current = null;
            return Task.FromResult(Deleted);
        }

        private void Record(CancellationToken cancellationToken)
        {
            Calls++;
            ReceivedTokens.Add(cancellationToken);
            if (Error is not null) throw Error;
        }
    }
}
