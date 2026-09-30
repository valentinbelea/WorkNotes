namespace WorkNotes.Business.Models;

public sealed record GitHubConfigurationInput(string ClientId, string? ClientSecret, string Scopes, string CallbackUrl);
