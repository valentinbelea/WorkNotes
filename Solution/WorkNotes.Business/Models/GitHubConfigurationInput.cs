namespace WorkNotes.Business.Models;

public sealed record GitHubConfigurationInput(string EnvironmentName, string ClientId, string? ClientSecret, string Scopes, string CallbackUrl);
