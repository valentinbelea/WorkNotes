using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Context;
using NoteBlockEntity = WorkNotes.DataAccess.Entities.NoteBlock;
using NoteEntity = WorkNotes.DataAccess.Entities.Note;

namespace WorkNotes.DataAccess.Repositories;

public sealed class NoteRepository(WorkNotesDbContext dbContext) : INoteRepository
{
    public async Task<IReadOnlyList<NoteSummary>> GetBoardAsync(string userId, int contextId, CancellationToken cancellationToken)
    {
        var rows = await VisibleTo(userId)
            .AsNoTracking()
            .Where(note => note.ContextId == contextId)
            .Select(note => new NoteSummary(note.Id, note.NoteType, note.Title, note.JournalDate,
                note.Visibility, note.CreatedAtUtc, note.OwnerUserId == userId))
            .ToListAsync(cancellationToken);
        return rows.Select(note => note with { CreatedAtUtc = Utc(note.CreatedAtUtc) }).ToList();
    }

    public async Task<NoteCreateStatus> AddAsync(NewNote note, CancellationToken cancellationToken)
    {
        var entity = new NoteEntity
        {
            ContextId = note.ContextId,
            OwnerUserId = note.OwnerUserId,
            NoteType = note.NoteType,
            Title = note.Title,
            JournalDate = note.JournalDate,
            Visibility = note.Visibility,
            CreatedByUserId = note.OwnerUserId,
            ModifiedByUserId = note.OwnerUserId
        };
        dbContext.Notes.Add(entity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return NoteCreateStatus.Created;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            // The context was deleted after the membership check.
            dbContext.Entry(entity).State = EntityState.Detached;
            return NoteCreateStatus.ContextNotFound;
        }
    }

    public async Task<NoteDocument?> GetDocumentAsync(int noteId, string userId, CancellationToken cancellationToken)
    {
        var document = await VisibleTo(userId)
            .AsNoTracking()
            .Where(note => note.Id == noteId)
            .Select(note => new
            {
                note.Id, note.ContextId, ContextName = note.Context.Name, note.NoteType, note.Title, note.Visibility,
                IsOwner = note.OwnerUserId == userId, note.CreatedAtUtc, note.RowVersion,
                Blocks = note.NoteBlocks.OrderBy(block => block.Position)
                    .Select(block => new NoteBlockDetails(block.Id, block.Content, block.CreatedAtUtc, block.ModifiedAtUtc))
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (document is null) return null;
        return new NoteDocument(document.Id, document.ContextId, document.ContextName, document.NoteType, document.Title,
            document.Visibility, document.IsOwner, Utc(document.CreatedAtUtc), Convert.ToBase64String(document.RowVersion),
            document.Blocks.Select(block => block with { CreatedAtUtc = Utc(block.CreatedAtUtc), ModifiedAtUtc = Utc(block.ModifiedAtUtc) }).ToList());
    }

    public async Task<NoteSaveResult> SaveAsync(NoteChanges changes, CancellationToken cancellationToken)
    {
        var note = await dbContext.Notes
            .Include(item => item.NoteBlocks)
            .SingleOrDefaultAsync(item => item.Id == changes.NoteId && item.OwnerUserId == changes.OwnerUserId && item.ArchivedAtUtc == null, cancellationToken);
        if (note is null) return new(NoteSaveStatus.NotFound);
        if (!TryReadVersion(changes.ExpectedVersion, out var expectedVersion)) return new(NoteSaveStatus.Conflict);
        // The note's rowversion guards the whole document: a save from a stale editor is rejected.
        dbContext.Entry(note).Property(item => item.RowVersion).OriginalValue = expectedVersion;

        var stored = note.NoteBlocks.ToDictionary(block => block.Id);
        var newIds = changes.Blocks.Select(block => block.Id).Where(id => !stored.ContainsKey(id)).ToList();
        // A new id must be new everywhere: ids of another note's paragraphs are never taken over.
        if (newIds.Count > 0 && await dbContext.NoteBlocks.AnyAsync(block => newIds.Contains(block.Id), cancellationToken))
            return new(NoteSaveStatus.InvalidContent);

        var changed = false;
        for (var position = 0; position < changes.Blocks.Count; position++)
        {
            var input = changes.Blocks[position];
            if (stored.Remove(input.Id, out var block))
            {
                // Moving a paragraph keeps its identity and audit; only a text change counts as a modification.
                if (block.Position != position) { block.Position = position; changed = true; }
                if (block.Content != input.Content)
                {
                    block.Content = input.Content;
                    block.ModifiedAtUtc = changes.SavedAtUtc;
                    block.ModifiedByUserId = changes.OwnerUserId;
                    changed = true;
                }
                continue;
            }
            note.NoteBlocks.Add(new NoteBlockEntity
            {
                Id = input.Id,
                Position = position,
                Content = input.Content,
                CreatedAtUtc = changes.SavedAtUtc,
                CreatedByUserId = changes.OwnerUserId,
                ModifiedAtUtc = changes.SavedAtUtc,
                ModifiedByUserId = changes.OwnerUserId
            });
            changed = true;
        }
        foreach (var removed in stored.Values)
        {
            dbContext.NoteBlocks.Remove(removed);
            changed = true;
        }
        if (note.Title != changes.Title) { note.Title = changes.Title; changed = true; }

        if (!changed)
            return note.RowVersion.AsSpan().SequenceEqual(expectedVersion) ? new(NoteSaveStatus.Saved, changes.ExpectedVersion) : new(NoteSaveStatus.Conflict);

        note.ModifiedAtUtc = changes.SavedAtUtc;
        note.ModifiedByUserId = changes.OwnerUserId;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(NoteSaveStatus.Saved, Convert.ToBase64String(note.RowVersion));
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return new(NoteSaveStatus.Conflict);
        }
    }

    // Every note requires membership of its context; shared notes are visible to all its members.
    private IQueryable<NoteEntity> VisibleTo(string userId) =>
        dbContext.Notes.Where(note => note.ArchivedAtUtc == null
            && note.Context.ContextMembers.Any(member => member.UserId == userId)
            && (note.OwnerUserId == userId || note.Visibility == NoteVisibilities.Context));

    private static bool TryReadVersion(string value, out byte[] version)
    {
        version = [];
        try
        {
            version = Convert.FromBase64String(value);
            return version.Length == 8;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // SQL Server returns datetime2 without a kind; the columns store UTC.
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
