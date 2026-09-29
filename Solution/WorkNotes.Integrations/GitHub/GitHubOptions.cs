namespace WorkNotes.Integrations.GitHub;

// The "GitHub" configuration section. ClientId and ClientSecret come from the registered GitHub App (recommended) or
// OAuth App and are never versioned: User Secrets in Development, environment variables (GitHub__ClientSecret) elsewhere.
// Scopes are used only by an OAuth App; a GitHub App takes its permissions from its own settings.
// The endpoints are GitHub.com's; they are settings so a GitHub Enterprise Server can be used later.
public sealed class GitHubOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Scopes { get; set; }
    public Uri AuthorizationEndpoint { get; set; } = new("https://github.com/login/oauth/authorize");
    public Uri TokenEndpoint { get; set; } = new("https://github.com/login/oauth/access_token");
    public Uri ApiBaseAddress { get; set; } = new("https://api.github.com/");
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);
}
