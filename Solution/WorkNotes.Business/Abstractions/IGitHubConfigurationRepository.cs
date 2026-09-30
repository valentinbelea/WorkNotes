using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface IGitHubConfigurationRepository
{
    Task<GitHubConfiguration?> GetAsync(CancellationToken cancellationToken);
    Task<GitHubConfigurationCredential?> GetCredentialAsync(CancellationToken cancellationToken);
    Task SaveAsync(GitHubConfigurationInput input, CancellationToken cancellationToken);
}
