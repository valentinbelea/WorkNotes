namespace WorkNotes.Business.Models;

public enum GitDisconnectStatus
{
    // The connection is deleted and its authorization revoked on GitHub.
    Disconnected,
    // The connection is deleted, but GitHub could not revoke the authorization: the user can revoke it in GitHub settings.
    NotRevoked,
    NotConnected
}
