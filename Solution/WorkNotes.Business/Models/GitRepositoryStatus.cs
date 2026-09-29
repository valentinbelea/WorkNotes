namespace WorkNotes.Business.Models;

public enum GitRepositoryStatus
{
    Succeeded,
    NotConfigured,
    // The user has not connected GitHub.
    NotConnected,
    ReconnectRequired,
    Unavailable,
    // The selection changed in another request while this one was saved.
    Conflict
}
