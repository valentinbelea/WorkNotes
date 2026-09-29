namespace WorkNotes.Business.Models;

// The repositories a token can see, read page by page; Truncated when there were more than GitRepositoryRules.MaxListed.
public sealed record GitRepositoryCatalog(IReadOnlyList<GitRepositoryInfo> Repositories, bool Truncated);
