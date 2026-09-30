using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface IGitHubConfigurationService
{
    Task<GitHubConfiguration?> GetAsync(CancellationToken cancellationToken);
    Task<GitHubConfigurationCredential?> GetCredentialAsync(CancellationToken cancellationToken);
    Task<GitHubConfigurationSaveStatus> SaveAsync(GitHubConfigurationInput input, CancellationToken cancellationToken);
}
