namespace WorkNotes.Business.Models;

// The import page: when Status is Succeeded, every repository GitHub lists for the user, then the imported ones it no
// longer lists, ordered by name; otherwise only the imported repositories, which cannot be changed until GitHub answers.
public sealed record GitRepositorySelection(GitRepositoryStatus Status, IReadOnlyList<GitRepositoryChoice> Repositories,
    bool Truncated);
