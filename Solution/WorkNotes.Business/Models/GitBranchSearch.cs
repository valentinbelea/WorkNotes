namespace WorkNotes.Business.Models;

// The branches of a repository whose name contains a reference, by name, at most GitReferenceRules.MaxBranchesShown.
// Truncated when the repository has more branches than were read, so there could be more.
public sealed record GitBranchSearch(GitReferenceStatus Status, IReadOnlyList<GitBranchInfo> Branches, bool Truncated = false)
{
    public static GitBranchSearch Failed(GitReferenceStatus status) => new(status, []);
}
