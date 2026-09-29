namespace WorkNotes.Business.Models;

public enum GitConnectStatus
{
    Connected,
    // GitHub:ClientId or GitHub:ClientSecret is missing from the configuration.
    NotConfigured,
    // The callback does not belong to a connection started by this user in this browser, or it came too late.
    InvalidState,
    // The user refused the authorization on GitHub.
    Denied,
    // GitHub refused the code or the token, or returned an account WorkNotes cannot store.
    Rejected,
    Unavailable
}
