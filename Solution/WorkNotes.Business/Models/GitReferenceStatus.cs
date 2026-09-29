namespace WorkNotes.Business.Models;

// The outcome of the Git reference operations of a note (INoteGitReferenceService).
public enum GitReferenceStatus
{
    Succeeded,
    // The note does not exist or the user may not see it.
    NotFound,
    // The user sees the note, but only its owner links branches.
    Forbidden,
    // The reference is not one of the configured types, or the paragraph does not write it, or the link does not exist.
    InvalidReference,
    // The repository is not among the ones the user imported.
    RepositoryNotFound,
    // The repository has no such branch, or its name does not contain the reference by the rules of the notes.
    BranchNotFound,
    NotConfigured,
    // The user has not connected GitHub.
    NotConnected,
    // The token was revoked or expired and cannot be refreshed, or GitHub refused the repository.
    ReconnectRequired,
    Unavailable
}
