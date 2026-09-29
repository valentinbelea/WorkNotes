namespace WorkNotes.Business.Models;

// The provider's account a token belongs to: its stable ID and its current login, which the user can rename.
public sealed record GitAccount(string Id, string Login);
