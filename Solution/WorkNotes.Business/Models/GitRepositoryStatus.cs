namespace WorkNotes.Business.Models;

public enum GitRepositoryStatus
{
    Succeeded,
    // The signed-in WorkNotes account has no email, so a personal GitHub connection cannot be associated.
    EmailRequired,
    NotConfigured,
    // The user has not connected GitHub.
    NotConnected,
    ReconnectRequired,
    Unavailable,
    // The selection changed in another request while this one was saved.
    Conflict
}
