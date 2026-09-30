namespace WorkNotes.Business.Models;

public sealed record GitHubConfigurationCredential(string ClientId, string ClientSecret, string Scopes, string CallbackUrl);
