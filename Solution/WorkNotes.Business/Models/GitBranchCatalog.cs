namespace WorkNotes.Business.Models;

// The branch names the provider lists for a repository, in its order; Truncated when it has more than were read
// (GitReferenceRules.MaxBranchesRead).
public sealed record GitBranchCatalog(IReadOnlyList<string> Names, bool Truncated);
