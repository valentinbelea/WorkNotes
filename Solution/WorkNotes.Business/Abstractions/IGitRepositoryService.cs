using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// The GitHub repositories a user imports in WorkNotes: the list comes from GitHub, in the user's name, and the choice is
// the user's own (dbo.GitRepositories).
public interface IGitRepositoryService
{
    // The repositories the user imported, by name, as they were stored; only the database is read (no call to GitHub).
    Task<IReadOnlyList<GitRepositoryInfo>> GetImportedAsync(string userId, CancellationToken cancellationToken);

    // Every repository GitHub lists for the user, each marked imported or not, plus the imported ones it no longer lists.
    Task<GitRepositorySelection> GetSelectionAsync(string userId, CancellationToken cancellationToken);

    // Makes the selected IDs the user's imported repositories: those GitHub lists are added or kept with its current
    // description, imported ones it no longer lists are kept only if still selected, the others are removed.
    // IDs that are neither listed nor imported are ignored. Nothing changes unless GitHub answers.
    Task<GitRepositoryStatus> SaveSelectionAsync(string userId, IReadOnlyCollection<string> selectedIds,
        CancellationToken cancellationToken);
}
