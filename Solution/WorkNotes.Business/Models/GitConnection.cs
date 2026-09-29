namespace WorkNotes.Business.Models;

// A connected Git account as the account page shows it: never with its tokens.
// AccessTokenExpiresAtUtc is null for a token that does not expire (an OAuth App token).
public sealed record GitConnection(
    string Provider,
    string AccountLogin,
    DateTime ConnectedAtUtc,
    DateTime ValidatedAtUtc,
    DateTime? AccessTokenExpiresAtUtc);
