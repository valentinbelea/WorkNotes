namespace WorkNotes.Business.Models;

public sealed record GitHubConfiguration(string EnvironmentName, string ClientId, bool HasClientSecret, string Scopes, string CallbackUrl);
