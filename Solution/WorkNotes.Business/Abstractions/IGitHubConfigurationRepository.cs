using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface IGitHubConfigurationRepository
{
    Task<GitHubConfiguration?> GetAsync(string environmentName, CancellationToken cancellationToken);
    Task<GitHubConfigurationCredential?> GetCredentialAsync(string environmentName, CancellationToken cancellationToken);
    Task SaveAsync(GitHubConfigurationInput input, CancellationToken cancellationToken);
}
