namespace WorkNotes.Business.Models;

public sealed record GitHubConfiguration(string ClientId, bool HasClientSecret, string Scopes, string CallbackUrl);
