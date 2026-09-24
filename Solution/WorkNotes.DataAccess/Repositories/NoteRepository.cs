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
        var rows = await SummaryRows(VisibleTo(userId).AsNoTracking().Where(note => note.ContextId == contextId), userId)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSummary).ToList();
    }

    public async Task<NoteSummary?> GetSummaryAsync(int noteId, string userId, CancellationToken cancellationToken)
    {
        var row = await SummaryRows(VisibleTo(userId).AsNoTracking().Where(note => note.Id == noteId), userId)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : ToSummary(row);
    }

    public async Task<bool> RenameAsync(int noteId, string ownerUserId, string? title, DateTime savedAtUtc, CancellationToken cancellationToken) =>
        // A single UPDATE: it also changes the rowversion, so an editor open elsewhere sees the change as a conflict.
        await dbContext.Notes
            .Where(note => note.Id == noteId && note.OwnerUserId == ownerUserId && note.ArchivedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(note => note.Title, title)
                .SetProperty(note => note.ModifiedAtUtc, savedAtUtc)
                .SetProperty(note => note.ModifiedByUserId, ownerUserId), cancellationToken) > 0;

    public async Task<bool> DeleteAsync(int noteId, string ownerUserId, CancellationToken cancellationToken) =>
        // FK_NoteBlocks_Notes_NoteId cascades: the paragraphs are deleted by SQL Server in the same statement.
        await dbContext.Notes
            .Where(note => note.Id == noteId && note.OwnerUserId == ownerUserId)
            .ExecuteDeleteAsync(cancellationToken) > 0;

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
        var saved = new List<NoteBlockEntity>(changes.Blocks.Count);
        for (var position = 0; position < changes.Blocks.Count; position++)
        {
            var input = changes.Blocks[position];
            if (stored.Remove(input.Id, out var block))
            {
                saved.Add(block);
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
            var added = new NoteBlockEntity
            {
                Id = input.Id,
                Position = position,
                Content = input.Content,
                CreatedAtUtc = changes.SavedAtUtc,
                CreatedByUserId = changes.OwnerUserId,
                ModifiedAtUtc = changes.SavedAtUtc,
                ModifiedByUserId = changes.OwnerUserId
            };
            note.NoteBlocks.Add(added);
            saved.Add(added);
            changed = true;
        }
        foreach (var removed in stored.Values)
        {
            dbContext.NoteBlocks.Remove(removed);
            changed = true;
        }
        if (note.Title != changes.Title) { note.Title = changes.Title; changed = true; }

        if (!changed)
            return note.RowVersion.AsSpan().SequenceEqual(expectedVersion)
                ? new(NoteSaveStatus.Saved, changes.ExpectedVersion, Audit(saved))
                : new(NoteSaveStatus.Conflict);

        note.ModifiedAtUtc = changes.SavedAtUtc;
        note.ModifiedByUserId = changes.OwnerUserId;
        // Always update the note row, even when its audit values are unchanged (two saves in the same second):
        // the UPDATE carries the rowversion check that rejects a save from a stale editor.
        dbContext.Entry(note).Property(item => item.ModifiedAtUtc).IsModified = true;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(NoteSaveStatus.Saved, Convert.ToBase64String(note.RowVersion), Audit(saved));
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return new(NoteSaveStatus.Conflict);
        }
    }

    // The card fields plus the start of the first paragraphs, read by SQL Server; the preview itself is built by NoteRules.
    private static IQueryable<SummaryRow> SummaryRows(IQueryable<NoteEntity> notes, string userId) =>
        notes.Select(note => new SummaryRow
        {
            Id = note.Id,
            ContextId = note.ContextId,
            NoteType = note.NoteType,
            Title = note.Title,
            JournalDate = note.JournalDate,
            Visibility = note.Visibility,
            CreatedAtUtc = note.CreatedAtUtc,
            ModifiedAtUtc = note.ModifiedAtUtc,
            IsOwner = note.OwnerUserId == userId,
            FirstParagraphs = note.NoteBlocks
                .OrderBy(block => block.Position)
                .Take(NoteRules.PreviewParagraphs)
                .Select(block => block.Content.Substring(0, NoteRules.PreviewSourceLength))
                .ToList()
        });

    // Notes.ModifiedAtUtc starts with the creation time (both columns default to SYSUTCDATETIME()),
    // so a note that was never changed is reported with no modification time.
    private static NoteSummary ToSummary(SummaryRow row) =>
        new(row.Id, row.ContextId, row.NoteType, row.Title, NoteRules.BuildPreview(row.FirstParagraphs), row.JournalDate,
            row.Visibility, Utc(row.CreatedAtUtc), row.ModifiedAtUtc > row.CreatedAtUtc ? Utc(row.ModifiedAtUtc) : null, row.IsOwner);

    private sealed class SummaryRow
    {
        public int Id { get; init; }
        public int ContextId { get; init; }
        public string NoteType { get; init; } = "";
        public string? Title { get; init; }
        public DateOnly? JournalDate { get; init; }
        public string Visibility { get; init; } = "";
        public DateTime CreatedAtUtc { get; init; }
        public DateTime ModifiedAtUtc { get; init; }
        public bool IsOwner { get; init; }
        public List<string> FirstParagraphs { get; init; } = [];
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

    private static IReadOnlyList<NoteBlockAudit> Audit(IEnumerable<NoteBlockEntity> blocks) =>
        blocks.Select(block => new NoteBlockAudit(block.Id, Utc(block.CreatedAtUtc), Utc(block.ModifiedAtUtc))).ToList();

    // SQL Server returns datetime2 without a kind; the columns store UTC.
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
