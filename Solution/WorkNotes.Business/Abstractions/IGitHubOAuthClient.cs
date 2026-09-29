using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// GitHub's OAuth endpoints and the account API, implemented in WorkNotes.Integrations. The calls report whether GitHub
// refused (Rejected) or could not answer (Unavailable); cancellation is propagated, never reported as a result.
public interface IGitHubOAuthClient
{
    // False when GitHub:ClientId or GitHub:ClientSecret is missing: no call can succeed.
    bool IsConfigured { get; }

    // The authorization address the browser is sent to, with the state and the PKCE challenge.
    string GetAuthorizationUrl(string state, string codeChallenge, string redirectUri);

    Task<GitProviderResult<GitTokens>> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri,
        CancellationToken cancellationToken);

    // Only for tokens that expire (a GitHub App): a refresh token is used once and replaced.
    Task<GitProviderResult<GitTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    // The account the token belongs to; Rejected when the token is revoked or expired.
    Task<GitProviderResult<GitAccount>> GetAccountAsync(string accessToken, CancellationToken cancellationToken);

    // The repositories the token can see (GET /user/repos: owned, collaborated and of the user's organizations, for a
    // GitHub App only those it is installed on), page by page up to GitRepositoryRules.MaxListed.
    Task<GitProviderResult<GitRepositoryCatalog>> GetRepositoriesAsync(string accessToken, CancellationToken cancellationToken);

    // The names of the branches of a repository (GET /repos/{full name}/branches), page by page up to
    // GitReferenceRules.MaxBranchesRead; NotFound when the token cannot see the repository.
    Task<GitProviderResult<GitBranchCatalog>> GetBranchesAsync(string accessToken, string repositoryFullName, CancellationToken cancellationToken);

    // One branch of a repository, with the name GitHub gives it (GET /repos/{full name}/git/ref/heads/{branch}); NotFound
    // when the repository has no such branch or the token cannot see it.
    Task<GitProviderResult<string>> GetBranchAsync(string accessToken, string repositoryFullName, string branchName,
        CancellationToken cancellationToken);

    // Revokes the user's authorization of the application, with all its tokens.
    Task<GitProviderStatus> RevokeAsync(string accessToken, CancellationToken cancellationToken);
}
