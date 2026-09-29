namespace WorkNotes.Business.Models;

// One row of the import page: IsImported is the stored choice; IsAccessible is false for an imported repository
// GitHub no longer lists for the user (deleted, made inaccessible or beyond the listed ones).
public sealed record GitRepositoryChoice(GitRepositoryInfo Repository, bool IsImported, bool IsAccessible);
