using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class GitHubConnectionService(IGitHubOAuthClient gitHub, IGitConnectionRepository connections, TimeProvider time)
    : IGitHubConnectionService
{
    private const string Provider = GitProviders.GitHub;

    public bool IsConfigured => gitHub.IsConfigured;

    public Task<GitConnection?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        return connections.GetAsync(userId, Provider, cancellationToken);
    }

    public GitHubAuthorization StartAuthorization(string redirectUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);
        if (!gitHub.IsConfigured) throw new InvalidOperationException("GitHub OAuth is not configured.");

        var pending = new GitHubPendingAuthorization(GitAuthorizationRules.NewState(), GitAuthorizationRules.NewCodeVerifier());
        var url = gitHub.GetAuthorizationUrl(pending.State, GitAuthorizationRules.CodeChallenge(pending.CodeVerifier), redirectUri);
        return new GitHubAuthorization(url, pending);
    }

    public async Task<GitConnectStatus> CompleteAuthorizationAsync(string userId, GitHubPendingAuthorization? pending,
        GitHubCallback callback, string redirectUri, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);
        cancellationToken.ThrowIfCancellationRequested();

        if (!gitHub.IsConfigured) return GitConnectStatus.NotConfigured;
        // The state is checked first: nothing in a callback this browser did not ask for is trusted, not even its error.
        if (pending is null || !GitAuthorizationRules.StateMatches(pending.State, callback.State))
            return GitConnectStatus.InvalidState;
        if (!string.IsNullOrEmpty(callback.Error))
            return callback.Error == "access_denied" ? GitConnectStatus.Denied : GitConnectStatus.Rejected;
        if (string.IsNullOrWhiteSpace(callback.Code)) return GitConnectStatus.Rejected;

        var grant = await gitHub.ExchangeCodeAsync(callback.Code, pending.CodeVerifier, redirectUri, cancellationToken);
        if (grant.Value is not { } tokens) return ConnectStatus(grant.Status);
        var account = await gitHub.GetAccountAsync(tokens.AccessToken, cancellationToken);
        if (account.Value is not { } owner) return ConnectStatus(account.Status);
        if (!GitAuthorizationRules.ValidAccount(owner)) return GitConnectStatus.Rejected;

        var now = Now();
        await connections.SaveAsync(userId, Provider,
            new GitCredential(owner.Id, owner.Login, Normalize(tokens), now, now), cancellationToken);
        return GitConnectStatus.Connected;
    }

    public async Task<GitVerifyStatus> VerifyAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!gitHub.IsConfigured) return GitVerifyStatus.NotConfigured;

        var credential = await connections.GetCredentialAsync(userId, Provider, cancellationToken);
        if (credential is null) return GitVerifyStatus.NotConnected;
        if (credential.Tokens is not { } tokens) return GitVerifyStatus.ReconnectRequired;

        var now = Now();
        if (GitAuthorizationRules.NeedsRefresh(tokens, now))
        {
            if (!GitAuthorizationRules.CanRefresh(tokens, now)) return GitVerifyStatus.ReconnectRequired;
            var refreshed = await gitHub.RefreshAsync(tokens.RefreshToken!, cancellationToken);
            if (refreshed.Status == GitProviderStatus.Unavailable) return GitVerifyStatus.Unavailable;
            if (refreshed.Value is { } fresh)
            {
                // The old refresh token is spent: the new tokens are saved before anything else can fail.
                credential = credential with { Tokens = Normalize(fresh) };
                if (!await connections.UpdateAsync(userId, Provider, credential, cancellationToken))
                    return GitVerifyStatus.NotConnected;
            }
            else
            {
                // A refresh token works once: a concurrent request may have refreshed and saved it first.
                var current = await connections.GetCredentialAsync(userId, Provider, cancellationToken);
                if (current is null) return GitVerifyStatus.NotConnected;
                if (current.Tokens is not { } saved || saved.AccessToken == tokens.AccessToken
                    || GitAuthorizationRules.NeedsRefresh(saved, now))
                    return GitVerifyStatus.ReconnectRequired;
                credential = current;
            }
        }

        var account = await gitHub.GetAccountAsync(credential.Tokens!.AccessToken, cancellationToken);
        if (account.Status == GitProviderStatus.Unavailable) return GitVerifyStatus.Unavailable;
        if (account.Value is not { } owner || !GitAuthorizationRules.ValidAccount(owner))
            return GitVerifyStatus.ReconnectRequired;

        var verified = credential with { AccountId = owner.Id, AccountLogin = owner.Login, ValidatedAtUtc = now };
        return await connections.UpdateAsync(userId, Provider, verified, cancellationToken)
            ? GitVerifyStatus.Valid
            : GitVerifyStatus.NotConnected;
    }

    public async Task<GitDisconnectStatus> DisconnectAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();

        var credential = await connections.GetCredentialAsync(userId, Provider, cancellationToken);
        if (credential is null) return GitDisconnectStatus.NotConnected;
        // Deleted first: the link leaves WorkNotes even when GitHub cannot be reached.
        if (!await connections.DeleteAsync(userId, Provider, cancellationToken)) return GitDisconnectStatus.NotConnected;
        if (!gitHub.IsConfigured || credential.Tokens is not { } tokens) return GitDisconnectStatus.NotRevoked;

        return await gitHub.RevokeAsync(tokens.AccessToken, cancellationToken) == GitProviderStatus.Succeeded
            ? GitDisconnectStatus.Disconnected
            : GitDisconnectStatus.NotRevoked;
    }

    private static GitConnectStatus ConnectStatus(GitProviderStatus status) =>
        status == GitProviderStatus.Unavailable ? GitConnectStatus.Unavailable : GitConnectStatus.Rejected;

    private static GitTokens Normalize(GitTokens tokens) =>
        tokens with { Scopes = GitAuthorizationRules.NormalizeScopes(tokens.Scopes) };

    // Audit moments are kept to the second, as the datetime2(0) columns store them.
    private DateTime Now()
    {
        var now = time.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }
}
