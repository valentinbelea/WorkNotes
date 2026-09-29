using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Context;
using GitRepositoryEntity = WorkNotes.DataAccess.Entities.GitRepository;

namespace WorkNotes.DataAccess.Repositories;

public sealed class GitRepositoryRepository(WorkNotesDbContext dbContext) : IGitRepositoryRepository
{
    public async Task<IReadOnlyList<GitRepository>> GetImportedAsync(string userId, string provider,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.GitRepositories
            .AsNoTracking()
            .Where(repository => repository.UserId == userId && repository.Provider == provider)
            .Select(repository => new
            {
                repository.ExternalId,
                repository.FullName,
                repository.Description,
                repository.IsPrivate,
                repository.DefaultBranch,
                repository.HtmlUrl,
                repository.ImportedAtUtc,
                repository.RefreshedAtUtc
            })
            .ToListAsync(cancellationToken);
        return rows
            .Select(row => new GitRepository(
                new GitRepositoryInfo(row.ExternalId, row.FullName, row.Description, row.IsPrivate, row.DefaultBranch, row.HtmlUrl),
                Utc(row.ImportedAtUtc), Utc(row.RefreshedAtUtc)))
            .ToList();
    }

    public async Task<bool> ReplaceAsync(string userId, string provider, IReadOnlyList<GitRepositoryInfo> repositories,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        var stored = await dbContext.GitRepositories
            .Where(repository => repository.UserId == userId && repository.Provider == provider)
            .ToListAsync(cancellationToken);
        var selected = repositories.ToDictionary(repository => repository.Id, StringComparer.Ordinal);

        foreach (var entity in stored)
        {
            if (selected.Remove(entity.ExternalId, out var info)) Apply(entity, info, nowUtc);
            else dbContext.GitRepositories.Remove(entity);
        }
        foreach (var info in selected.Values)
        {
            var entity = new GitRepositoryEntity { UserId = userId, Provider = provider, ExternalId = info.Id, ImportedAtUtc = nowUtc };
            Apply(entity, info, nowUtc);
            dbContext.GitRepositories.Add(entity);
        }

        try
        {
            // One SaveChanges: the deletions, updates and insertions are a single transaction.
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException
            || ex is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } })
        {
            // Another request of the same user saved its selection first (a row it added or removed).
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    private static void Apply(GitRepositoryEntity entity, GitRepositoryInfo info, DateTime nowUtc)
    {
        entity.FullName = info.FullName;
        entity.Description = info.Description;
        entity.IsPrivate = info.IsPrivate;
        entity.DefaultBranch = info.DefaultBranch;
        entity.HtmlUrl = info.HtmlUrl;
        entity.RefreshedAtUtc = nowUtc;
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
