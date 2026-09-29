namespace WorkNotes.Business.Models;

// A stored connection with its tokens, for GitHubConnectionService only. Tokens is null when the stored tokens can no
// longer be decrypted (the application's Data Protection keys changed): the user has to connect again.
public sealed record GitCredential(
    string AccountId,
    string AccountLogin,
    GitTokens? Tokens,
    DateTime ConnectedAtUtc,
    DateTime ValidatedAtUtc);
