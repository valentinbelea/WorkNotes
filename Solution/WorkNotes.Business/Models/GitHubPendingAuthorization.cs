namespace WorkNotes.Business.Models;

// What the browser keeps between leaving for GitHub and coming back: the state that ties the callback to this request
// (CSRF protection) and the PKCE code verifier the authorization code is exchanged with.
public sealed record GitHubPendingAuthorization(string State, string CodeVerifier);
