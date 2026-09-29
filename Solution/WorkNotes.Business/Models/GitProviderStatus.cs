namespace WorkNotes.Business.Models;

// The outcome of a call to a Git provider: Rejected means the provider answered and refused (an invalid code or token),
// Unavailable that it could not be reached or answered with an error of its own; the two are never confused.
public enum GitProviderStatus
{
    Succeeded,
    Rejected,
    Unavailable
}
