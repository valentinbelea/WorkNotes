using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// The repositories a user imported, in dbo.GitRepositories: a user reads and changes only their own.
public interface IGitRepositoryRepository
{
    // The user's imported repositories of the provider, in any order; empty when there are none.
    Task<IReadOnlyList<GitRepository>> GetImportedAsync(string userId, string provider, CancellationToken cancellationToken);

    // Makes the user's imported repositories of the provider exactly the given ones, in one transaction: removes the
    // others, updates the description of the kept ones and adds the new ones (ImportedAtUtc = RefreshedAtUtc = nowUtc).
    // False when another request changed the selection meanwhile; nothing is saved then.
    Task<bool> ReplaceAsync(string userId, string provider, IReadOnlyList<GitRepositoryInfo> repositories, DateTime nowUtc,
        CancellationToken cancellationToken);
}
