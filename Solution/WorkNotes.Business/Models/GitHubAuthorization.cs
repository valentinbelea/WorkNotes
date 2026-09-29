namespace WorkNotes.Business.Models;

// The start of a GitHub connection: the address the browser is sent to and what it must keep until the callback.
public sealed record GitHubAuthorization(string Url, GitHubPendingAuthorization Pending);
