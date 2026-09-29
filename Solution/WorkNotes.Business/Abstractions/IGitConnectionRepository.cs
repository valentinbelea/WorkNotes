using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// The Git connections in dbo.GitConnections, always of one user: a user reads and changes only their own.
// The implementation encrypts the tokens before saving them and decrypts them when reading.
public interface IGitConnectionRepository
{
    // Null when the user has no connection to the provider.
    Task<GitConnection?> GetAsync(string userId, string provider, CancellationToken cancellationToken);

    // Null when the user has no connection to the provider; Tokens is null when they cannot be decrypted.
    Task<GitCredential?> GetCredentialAsync(string userId, string provider, CancellationToken cancellationToken);

    // Adds the connection or replaces the user's existing one to the provider.
    Task SaveAsync(string userId, string provider, GitCredential credential, CancellationToken cancellationToken);

    // Changes an existing connection; false when it no longer exists (it was disconnected meanwhile).
    Task<bool> UpdateAsync(string userId, string provider, GitCredential credential, CancellationToken cancellationToken);

    // False when there was nothing to delete.
    Task<bool> DeleteAsync(string userId, string provider, CancellationToken cancellationToken);
}
