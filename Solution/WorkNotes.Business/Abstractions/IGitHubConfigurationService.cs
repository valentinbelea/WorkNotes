using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface IGitHubConfigurationService
{
    string CurrentEnvironmentName { get; }
    Task<GitHubConfiguration?> GetAsync(string environmentName, CancellationToken cancellationToken);
    Task<GitHubConfigurationCredential?> GetCredentialAsync(CancellationToken cancellationToken);
    Task<GitHubConfigurationSaveStatus> SaveAsync(GitHubConfigurationInput input, CancellationToken cancellationToken);
}
