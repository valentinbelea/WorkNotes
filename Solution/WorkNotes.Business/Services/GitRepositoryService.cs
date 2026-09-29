using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class GitRepositoryService(IGitHubTokenService tokens, IGitHubOAuthClient gitHub,
    IGitRepositoryRepository repositories, TimeProvider time) : IGitRepositoryService
{
    private const string Provider = GitProviders.GitHub;

    public async Task<IReadOnlyList<GitRepositoryInfo>> GetImportedAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var imported = await repositories.GetImportedAsync(userId, Provider, cancellationToken);
        return imported.Select(repository => repository.Info)
            .OrderBy(info => info.FullName, StringComparer.OrdinalIgnoreCase).ThenBy(info => info.Id, StringComparer.Ordinal).ToList();
    }

    public async Task<GitRepositorySelection> GetSelectionAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();

        var imported = await repositories.GetImportedAsync(userId, Provider, cancellationToken);
        var (status, catalog) = await ListAsync(userId, cancellationToken);
        if (catalog is null)
            return new GitRepositorySelection(status,
                Ordered(imported.Select(repository => new GitRepositoryChoice(repository.Info, true, true))), false);

        var importedIds = imported.Select(repository => repository.Info.Id).ToHashSet(StringComparer.Ordinal);
        var listedIds = catalog.Repositories.Select(repository => repository.Id).ToHashSet(StringComparer.Ordinal);
        var choices = catalog.Repositories
            .Select(repository => new GitRepositoryChoice(repository, importedIds.Contains(repository.Id), true))
            .Concat(imported.Where(repository => !listedIds.Contains(repository.Info.Id))
                .Select(repository => new GitRepositoryChoice(repository.Info, true, false)));
        return new GitRepositorySelection(GitRepositoryStatus.Succeeded, Ordered(choices), catalog.Truncated);
    }

    public async Task<GitRepositoryStatus> SaveSelectionAsync(string userId, IReadOnlyCollection<string> selectedIds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(selectedIds);
        cancellationToken.ThrowIfCancellationRequested();

        var (status, catalog) = await ListAsync(userId, cancellationToken);
        if (catalog is null) return status;
        var imported = await repositories.GetImportedAsync(userId, Provider, cancellationToken);

        // The form only names IDs: what is stored comes from GitHub, or from the earlier import for a repository
        // GitHub no longer lists.
        var known = imported.ToDictionary(repository => repository.Info.Id, repository => repository.Info, StringComparer.Ordinal);
        foreach (var repository in catalog.Repositories) known[repository.Id] = repository;
        var selected = selectedIds
            .Where(GitRepositoryRules.ValidId)
            .Distinct(StringComparer.Ordinal)
            .Select(id => known.GetValueOrDefault(id))
            .OfType<GitRepositoryInfo>()
            .ToList();

        return await repositories.ReplaceAsync(userId, Provider, selected, Now(), cancellationToken)
            ? GitRepositoryStatus.Succeeded
            : GitRepositoryStatus.Conflict;
    }

    // The repositories GitHub lists for the user, normalized; null with the reason when they cannot be read.
    private async Task<(GitRepositoryStatus Status, GitRepositoryCatalog? Catalog)> ListAsync(string userId,
        CancellationToken cancellationToken)
    {
        var token = await tokens.GetAsync(userId, cancellationToken);
        if (token.Credential?.Tokens is not { } current) return (Status(token.Status), null);

        var listed = await gitHub.GetRepositoriesAsync(current.AccessToken, cancellationToken);
        if (listed.Value is not { } catalog)
            return (listed.Status == GitProviderStatus.Unavailable
                ? GitRepositoryStatus.Unavailable
                : GitRepositoryStatus.ReconnectRequired, null);

        var normalized = catalog.Repositories
            .Select(GitRepositoryRules.Normalize)
            .OfType<GitRepositoryInfo>()
            .DistinctBy(repository => repository.Id, StringComparer.Ordinal)
            .ToList();
        return (GitRepositoryStatus.Succeeded, catalog with { Repositories = normalized });
    }

    private static GitRepositoryStatus Status(GitVerifyStatus status) => status switch
    {
        GitVerifyStatus.NotConfigured => GitRepositoryStatus.NotConfigured,
        GitVerifyStatus.NotConnected => GitRepositoryStatus.NotConnected,
        GitVerifyStatus.Unavailable => GitRepositoryStatus.Unavailable,
        _ => GitRepositoryStatus.ReconnectRequired
    };

    private static List<GitRepositoryChoice> Ordered(IEnumerable<GitRepositoryChoice> choices) =>
        choices.OrderBy(choice => choice.Repository.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.Repository.Id, StringComparer.Ordinal)
            .ToList();

    private DateTime Now()
    {
        var now = time.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }
}
