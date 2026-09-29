namespace WorkNotes.Business.Models;

// A repository imported in WorkNotes by a user: its stored description and when it was imported and last read.
public sealed record GitRepository(GitRepositoryInfo Info, DateTime ImportedAtUtc, DateTime RefreshedAtUtc);
