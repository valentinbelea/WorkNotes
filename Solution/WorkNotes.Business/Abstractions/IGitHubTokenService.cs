using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// A usable GitHub access token of one user, for the Business services that call GitHub in the user's name
// (GitHubConnectionService, GitRepositoryService); it is never given to Web.
public interface IGitHubTokenService
{
    // Refreshes the token first when it expires within GitAuthorizationRules.RefreshMargin and saves the new tokens.
    Task<GitAccessToken> GetAsync(string userId, CancellationToken cancellationToken);
}
