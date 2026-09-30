namespace WorkNotes.Business.Models;

public enum GitConnectStatus
{
    Connected,
    // The global GitHub OAuth configuration is missing or cannot be decrypted.
    NotConfigured,
    // The callback does not belong to a connection started by this user in this browser, or it came too late.
    InvalidState,
    // The user refused the authorization on GitHub.
    Denied,
    // GitHub refused the code or the token, or returned an account WorkNotes cannot store.
    Rejected,
    Unavailable
}
