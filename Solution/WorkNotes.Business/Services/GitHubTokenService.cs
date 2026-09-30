using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class GitHubTokenService(IGitHubOAuthClient gitHub, IGitConnectionRepository connections, TimeProvider time)
    : IGitHubTokenService
{
    private const string Provider = GitProviders.GitHub;

    public async Task<GitAccessToken> GetAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!await gitHub.IsConfiguredAsync(cancellationToken)) return GitAccessToken.Failed(GitVerifyStatus.NotConfigured);

        var credential = await connections.GetCredentialAsync(userId, Provider, cancellationToken);
        if (credential is null) return GitAccessToken.Failed(GitVerifyStatus.NotConnected);
        if (credential.Tokens is not { } tokens) return GitAccessToken.Failed(GitVerifyStatus.ReconnectRequired);

        var now = time.GetUtcNow().UtcDateTime;
        if (!GitAuthorizationRules.NeedsRefresh(tokens, now)) return new GitAccessToken(GitVerifyStatus.Valid, credential);
        if (!GitAuthorizationRules.CanRefresh(tokens, now)) return GitAccessToken.Failed(GitVerifyStatus.ReconnectRequired);

        var refreshed = await gitHub.RefreshAsync(tokens.RefreshToken!, cancellationToken);
        if (refreshed.Status == GitProviderStatus.Unavailable) return GitAccessToken.Failed(GitVerifyStatus.Unavailable);
        if (refreshed.Value is { } fresh)
        {
            // The old refresh token is spent: the new tokens are saved before anything else can fail.
            credential = credential with { Tokens = fresh with { Scopes = GitAuthorizationRules.NormalizeScopes(fresh.Scopes) } };
            return await connections.UpdateAsync(userId, Provider, credential, cancellationToken)
                ? new GitAccessToken(GitVerifyStatus.Valid, credential)
                : GitAccessToken.Failed(GitVerifyStatus.NotConnected);
        }

        // A refresh token works once: a concurrent request may have refreshed and saved it first.
        var current = await connections.GetCredentialAsync(userId, Provider, cancellationToken);
        if (current is null) return GitAccessToken.Failed(GitVerifyStatus.NotConnected);
        return current.Tokens is not { } saved || saved.AccessToken == tokens.AccessToken
            || GitAuthorizationRules.NeedsRefresh(saved, now)
            ? GitAccessToken.Failed(GitVerifyStatus.ReconnectRequired)
            : new GitAccessToken(GitVerifyStatus.Valid, current);
    }
}
