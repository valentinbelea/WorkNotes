namespace WorkNotes.Business.Models;

public enum GitVerifyStatus
{
    Valid,
    NotConnected,
    NotConfigured,
    // The token was revoked or expired and cannot be refreshed, or it can no longer be decrypted.
    ReconnectRequired,
    Unavailable
}
