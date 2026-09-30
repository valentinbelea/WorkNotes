namespace WorkNotes.Integrations.GitHub;

// Non-sensitive transport settings from the "GitHub" configuration section. OAuth credentials, scopes and callback URL
// are read from GitHubConfigurations for every OAuth operation.
public sealed class GitHubOptions
{
    public Uri AuthorizationEndpoint { get; set; } = new("https://github.com/login/oauth/authorize");
    public Uri TokenEndpoint { get; set; } = new("https://github.com/login/oauth/access_token");
    public Uri ApiBaseAddress { get; set; } = new("https://api.github.com/");
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);
}
