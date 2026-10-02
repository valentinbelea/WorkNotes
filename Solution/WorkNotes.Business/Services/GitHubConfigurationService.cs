using System.Text.RegularExpressions;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed partial class GitHubConfigurationService(IGitHubConfigurationRepository repository, IRuntimeEnvironment environment) : IGitHubConfigurationService
{
    public string CurrentEnvironmentName => environment.Name;
    public Task<GitHubConfiguration?> GetAsync(string environmentName, CancellationToken cancellationToken) =>
        repository.GetAsync(environmentName, cancellationToken);
    public Task<GitHubConfigurationCredential?> GetCredentialAsync(CancellationToken cancellationToken) =>
        repository.GetCredentialAsync(CurrentEnvironmentName, cancellationToken);

    public async Task<GitHubConfigurationSaveStatus> SaveAsync(GitHubConfigurationInput input, CancellationToken cancellationToken)
    {
        var environmentName = input.EnvironmentName.Trim();
        if (!GitHubEnvironments.IsSupported(environmentName)) return GitHubConfigurationSaveStatus.InvalidEnvironment;
        var clientId = input.ClientId.Trim();
        var scopes = input.Scopes.Trim();
        var callbackUrl = input.CallbackUrl.Trim();
        if (clientId.Length == 0) return GitHubConfigurationSaveStatus.ClientIdRequired;
        if (scopes.Length == 0 || !ScopesPattern().IsMatch(scopes)) return GitHubConfigurationSaveStatus.InvalidScopes;
        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            return GitHubConfigurationSaveStatus.InvalidCallbackUrl;

        var current = await repository.GetAsync(environmentName, cancellationToken);
        if (current is null && string.IsNullOrWhiteSpace(input.ClientSecret))
            return GitHubConfigurationSaveStatus.ClientSecretRequired;

        await repository.SaveAsync(new(environmentName, clientId, string.IsNullOrWhiteSpace(input.ClientSecret) ? null : input.ClientSecret,
            scopes, callbackUrl), cancellationToken);
        return GitHubConfigurationSaveStatus.Succeeded;
    }

    [GeneratedRegex(@"^[A-Za-z0-9:_-]*(?:[ ,]+[A-Za-z0-9:_-]+)*$")]
    private static partial Regex ScopesPattern();
}
