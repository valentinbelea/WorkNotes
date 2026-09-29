using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Context;

namespace WorkNotes.DataAccess.Repositories;

public sealed class NoteGitReferenceRepository(WorkNotesDbContext dbContext) : INoteGitReferenceRepository
{
    public Task<bool?> IsOwnerAsync(int noteId, string userId, CancellationToken cancellationToken) =>
        dbContext.VisibleNotes(userId)
            .AsNoTracking()
            .Where(note => note.Id == noteId)
            .Select(note => (bool?)(note.OwnerUserId == userId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<StoredNoteGitReference>> GetAsync(int noteId, string userId, CancellationToken cancellationToken)
    {
        var visible = dbContext.VisibleNotes(userId);
        var rows = await dbContext.NoteBlockGitReferences
            .AsNoTracking()
            .Where(link => link.NoteBlock.NoteId == noteId && visible.Any(note => note.Id == noteId))
            .OrderBy(link => link.NoteBlock.Position).ThenBy(link => link.Id)
            .Select(link => new
            {
                link.Id,
                link.NoteBlockId,
                link.NoteBlock.Position,
                link.NoteBlock.Content,
                link.WorkReference.NormalizedReference,
                link.GitReference.RepositoryFullName,
                link.GitReference.RepositoryUrl,
                link.GitReference.Name
            })
            .ToListAsync(cancellationToken);
        return rows
            .Select(row => new StoredNoteGitReference(row.Id, row.NoteBlockId, row.Position, row.Content, row.NormalizedReference,
                row.RepositoryFullName, row.RepositoryUrl, row.Name))
            .ToList();
    }

    public Task<string?> GetBlockContentAsync(int noteId, string ownerUserId, Guid blockId, CancellationToken cancellationToken) =>
        dbContext.NoteBlocks
            .AsNoTracking()
            .Where(block => block.Id == blockId && block.NoteId == noteId && block.Note.OwnerUserId == ownerUserId && block.Note.ArchivedAtUtc == null)
            .Select(block => (string?)block.Content)
            .SingleOrDefaultAsync(cancellationToken);

    // The link is added by SQL, like the references of the catalog: each row with UPDLOCK and HOLDLOCK, so two requests
    // adding the same branch or the same link at once add it once. All in one transaction: a paragraph deleted meanwhile
    // (foreign key 547) leaves nothing behind.
    public async Task<bool> AddAsync(int noteId, NewNoteGitReference reference, CancellationToken cancellationToken)
    {
        var owned = await dbContext.NoteBlocks.AnyAsync(block => block.Id == reference.NoteBlockId && block.NoteId == noteId
            && block.Note.OwnerUserId == reference.CreatedByUserId && block.Note.ArchivedAtUtc == null, cancellationToken);
        if (!owned) return false;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var normalized = reference.NormalizedReference;
            await dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO [dbo].[WorkReferences] ([ReferenceType], [ReferenceNumber], [NormalizedReference])
                SELECT {reference.ReferenceType}, {reference.ReferenceNumber}, {normalized}
                WHERE NOT EXISTS (SELECT 1 FROM [dbo].[WorkReferences] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [ReferenceType] = {reference.ReferenceType} AND [ReferenceNumber] = {reference.ReferenceNumber})
                """, cancellationToken);
            var workReferenceId = await dbContext.WorkReferences
                .Where(work => work.NormalizedReference == normalized)
                .Select(work => work.Id)
                .SingleAsync(cancellationToken);

            // A known branch takes the current name and address of its repository; the name is compared exactly (the
            // column is binary).
            await dbContext.Database.ExecuteSqlAsync($"""
                UPDATE [dbo].[GitReferences] WITH (UPDLOCK, HOLDLOCK)
                SET [RepositoryFullName] = {reference.RepositoryFullName}, [RepositoryUrl] = {reference.RepositoryUrl}
                WHERE [Provider] = {reference.Provider} AND [RepositoryExternalId] = {reference.RepositoryId}
                    AND [Kind] = {GitReferenceKinds.Branch} AND [Name] = {reference.BranchName}
                IF @@ROWCOUNT = 0
                    INSERT INTO [dbo].[GitReferences] ([Provider], [RepositoryExternalId], [RepositoryFullName], [RepositoryUrl], [Kind], [Name], [CreatedAtUtc])
                    VALUES ({reference.Provider}, {reference.RepositoryId}, {reference.RepositoryFullName}, {reference.RepositoryUrl},
                        {GitReferenceKinds.Branch}, {reference.BranchName}, {reference.CreatedAtUtc})
                """, cancellationToken);
            var gitReferenceId = await dbContext.GitReferences
                .Where(git => git.Provider == reference.Provider && git.RepositoryExternalId == reference.RepositoryId
                    && git.Kind == GitReferenceKinds.Branch && git.Name == reference.BranchName)
                .Select(git => git.Id)
                .SingleAsync(cancellationToken);

            await dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO [dbo].[NoteBlockGitReferences] ([NoteBlockId], [GitReferenceId], [WorkReferenceId], [CreatedAtUtc], [CreatedByUserId])
                SELECT {reference.NoteBlockId}, {gitReferenceId}, {workReferenceId}, {reference.CreatedAtUtc}, {reference.CreatedByUserId}
                WHERE NOT EXISTS (SELECT 1 FROM [dbo].[NoteBlockGitReferences] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [NoteBlockId] = {reference.NoteBlockId} AND [GitReferenceId] = {gitReferenceId} AND [WorkReferenceId] = {workReferenceId})
                """, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            // The paragraph was deleted after it was checked: the transaction is disposed without a commit, so nothing is saved.
            return false;
        }
    }

    public async Task<bool> RemoveAsync(int noteId, string ownerUserId, int linkId, CancellationToken cancellationToken) =>
        await dbContext.NoteBlockGitReferences
            .Where(link => link.Id == linkId && link.NoteBlock.NoteId == noteId && link.NoteBlock.Note.OwnerUserId == ownerUserId
                && link.NoteBlock.Note.ArchivedAtUtc == null)
            .ExecuteDeleteAsync(cancellationToken) > 0;
}
