using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class NoteGitReferenceService(INoteGitReferenceRepository links, IGitRepositoryRepository repositories,
    IGitHubTokenService tokens, IGitHubOAuthClient gitHub, IReferenceTypeService types, TimeProvider time) : INoteGitReferenceService
{
    private const string Provider = GitProviders.GitHub;

    public async Task<GitBranchSearch> SearchBranchesAsync(string userId, int noteId, string repositoryId, string normalizedReference,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (await CheckOwnerAsync(userId, noteId, cancellationToken) is { } refused) return GitBranchSearch.Failed(refused);
        var parser = await types.GetParserAsync(cancellationToken);
        if (GitReferenceRules.ParseReference(parser, normalizedReference) is null) return GitBranchSearch.Failed(GitReferenceStatus.InvalidReference);
        var repository = await FindRepositoryAsync(userId, repositoryId, cancellationToken);
        if (repository is null) return GitBranchSearch.Failed(GitReferenceStatus.RepositoryNotFound);
        var token = await tokens.GetAsync(userId, cancellationToken);
        if (token.Credential?.Tokens is not { } current) return GitBranchSearch.Failed(Status(token.Status));

        var listed = await gitHub.GetBranchesAsync(current.AccessToken, repository.FullName, cancellationToken);
        if (listed.Value is not { } catalog) return GitBranchSearch.Failed(Status(listed.Status, GitReferenceStatus.RepositoryNotFound));

        var found = catalog.Names
            .Where(GitReferenceRules.ValidBranchName)
            .Where(name => GitReferenceRules.BranchMatches(parser, name, normalizedReference))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ThenBy(name => name, StringComparer.Ordinal)
            .Take(GitReferenceRules.MaxBranchesShown)
            .Select(name => new GitBranchInfo(name, GitReferenceRules.BranchUrl(repository.HtmlUrl, name)))
            .ToList();
        return new GitBranchSearch(GitReferenceStatus.Succeeded, found, catalog.Truncated);
    }

    public async Task<NoteGitReferenceResult> AddAsync(string userId, int noteId, Guid blockId, string repositoryId, string normalizedReference,
        string branchName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (await CheckOwnerAsync(userId, noteId, cancellationToken) is { } refused) return new(refused);
        var parser = await types.GetParserAsync(cancellationToken);
        if (GitReferenceRules.ParseReference(parser, normalizedReference) is not { } reference) return new(GitReferenceStatus.InvalidReference);
        if (!GitReferenceRules.ValidBranchName(branchName)) return new(GitReferenceStatus.BranchNotFound);

        // The paragraph must be the note's and must write the reference: a link is a branch of a reference of the text.
        var content = await links.GetBlockContentAsync(noteId, userId, blockId, cancellationToken);
        if (content is null || !GitReferenceRules.Writes(parser, content, normalizedReference)) return new(GitReferenceStatus.InvalidReference);

        var repository = await FindRepositoryAsync(userId, repositoryId, cancellationToken);
        if (repository is null) return new(GitReferenceStatus.RepositoryNotFound);
        var token = await tokens.GetAsync(userId, cancellationToken);
        if (token.Credential?.Tokens is not { } current) return new(Status(token.Status));

        // GitHub says whether the branch exists and how its name is written; that name must contain the reference.
        var branch = await gitHub.GetBranchAsync(current.AccessToken, repository.FullName, branchName, cancellationToken);
        if (branch.Value is not { } name) return new(Status(branch.Status, GitReferenceStatus.BranchNotFound));
        if (!GitReferenceRules.ValidBranchName(name) || !GitReferenceRules.BranchMatches(parser, name, normalizedReference))
            return new(GitReferenceStatus.BranchNotFound);

        var added = await links.AddAsync(noteId, new NewNoteGitReference(blockId, reference.ReferenceType, reference.ReferenceNumber, Provider,
            repository.Id, repository.FullName, repository.HtmlUrl, name, userId, Now()), cancellationToken);
        return added ? new(GitReferenceStatus.Succeeded, await GetAsync(userId, noteId, cancellationToken)) : new(GitReferenceStatus.InvalidReference);
    }

    public async Task<NoteGitReferenceResult> RemoveAsync(string userId, int noteId, int linkId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (await CheckOwnerAsync(userId, noteId, cancellationToken) is { } refused) return new(refused);
        if (!await links.RemoveAsync(noteId, userId, linkId, cancellationToken)) return new(GitReferenceStatus.InvalidReference);
        return new(GitReferenceStatus.Succeeded, await GetAsync(userId, noteId, cancellationToken));
    }

    public async Task<IReadOnlyList<NoteGitReference>> GetAsync(string userId, int noteId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var stored = await links.GetAsync(noteId, userId, cancellationToken);
        if (stored.Count == 0) return [];
        var parser = await types.GetParserAsync(cancellationToken);
        return stored
            // A link shows while its paragraph writes its reference; the address of a branch is always a https one.
            .Where(link => GitReferenceRules.Writes(parser, link.BlockContent, link.NormalizedReference) && IsHttps(link.RepositoryUrl))
            .OrderBy(link => link.BlockPosition).ThenBy(link => link.NormalizedReference, StringComparer.Ordinal)
            .ThenBy(link => link.RepositoryFullName, StringComparer.OrdinalIgnoreCase).ThenBy(link => link.Name, StringComparer.Ordinal)
            .Select(link => new NoteGitReference(link.Id, link.NoteBlockId, link.NormalizedReference, link.RepositoryFullName, link.Name,
                GitReferenceRules.BranchUrl(link.RepositoryUrl, link.Name)))
            .ToList();
    }

    // Null when the user owns the note; otherwise why they may not link branches to it.
    private async Task<GitReferenceStatus?> CheckOwnerAsync(string userId, int noteId, CancellationToken cancellationToken)
    {
        var isOwner = await links.IsOwnerAsync(noteId, userId, cancellationToken);
        if (isOwner is null) return GitReferenceStatus.NotFound;
        // Members may read a shared note; only its owner writes in it.
        return isOwner == true ? null : GitReferenceStatus.Forbidden;
    }

    private async Task<GitRepositoryInfo?> FindRepositoryAsync(string userId, string repositoryId, CancellationToken cancellationToken)
    {
        if (!GitRepositoryRules.ValidId(repositoryId)) return null;
        var imported = await repositories.GetImportedAsync(userId, Provider, cancellationToken);
        return imported.Select(repository => repository.Info).FirstOrDefault(info => info.Id == repositoryId);
    }

    private static bool IsHttps(string url) => Uri.TryCreate(url, UriKind.Absolute, out var address) && address.Scheme == Uri.UriSchemeHttps;

    // A token that cannot be used: the user has to connect (again) or wait.
    private static GitReferenceStatus Status(GitVerifyStatus status) => status switch
    {
        GitVerifyStatus.NotConfigured => GitReferenceStatus.NotConfigured,
        GitVerifyStatus.NotConnected => GitReferenceStatus.NotConnected,
        GitVerifyStatus.Unavailable => GitReferenceStatus.Unavailable,
        _ => GitReferenceStatus.ReconnectRequired
    };

    // A failed call to GitHub: it has no such repository or branch (notFound), refused the token, or did not answer.
    private static GitReferenceStatus Status(GitProviderStatus status, GitReferenceStatus notFound) => status switch
    {
        GitProviderStatus.NotFound => notFound,
        GitProviderStatus.Unavailable => GitReferenceStatus.Unavailable,
        _ => GitReferenceStatus.ReconnectRequired
    };

    private DateTime Now()
    {
        var now = time.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }
}
