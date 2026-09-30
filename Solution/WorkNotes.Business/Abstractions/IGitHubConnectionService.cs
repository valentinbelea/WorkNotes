using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// The connection of a WorkNotes user to their GitHub account through OAuth (authorization code with PKCE): one
// connection per user, which only that user sees and changes.
public interface IGitHubConnectionService
{
    // False when GitHub OAuth is not configured: the account page says so and offers no connection.
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken);

    // Null when the user has not connected GitHub.
    Task<GitConnection?> GetAsync(string userId, CancellationToken cancellationToken);

    // A new state and PKCE verifier and the GitHub address to send the browser to. Throws when not configured.
    Task<GitHubAuthorization?> StartAuthorizationAsync(CancellationToken cancellationToken);

    // Checks the callback against the pending authorization kept by the browser (null when it is missing or expired),
    // exchanges the code, reads the account and saves the connection, replacing an earlier one.
    Task<GitConnectStatus> CompleteAuthorizationAsync(string userId, GitHubPendingAuthorization? pending,
        GitHubCallback callback, CancellationToken cancellationToken);

    // Checks the stored token with GitHub, refreshing it first when it expires, and updates the account's login.
    Task<GitVerifyStatus> VerifyAsync(string userId, CancellationToken cancellationToken);

    // Deletes the connection, then revokes its authorization on GitHub.
    Task<GitDisconnectStatus> DisconnectAsync(string userId, CancellationToken cancellationToken);
}
