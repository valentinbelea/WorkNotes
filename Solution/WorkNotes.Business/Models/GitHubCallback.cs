namespace WorkNotes.Business.Models;

// The query GitHub sends the browser back with: a code and the state, or an error (access_denied when the user refused).
public sealed record GitHubCallback(string? Code, string? State, string? Error);
