using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Context;
using NoteBlockEntity = WorkNotes.DataAccess.Entities.NoteBlock;
using NoteEntity = WorkNotes.DataAccess.Entities.Note;
using NoteReferenceEntity = WorkNotes.DataAccess.Entities.NoteReference;

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

    public async Task<bool> DeleteAsync(int noteId, string ownerUserId, CancellationToken cancellationToken)
    {
        // Serializable: no other note can add a reference to this one between the two statements.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // The references other notes have to it go first (FK_NoteReferences_Notes_TargetNoteId has no cascade); their text
        // keeps the number, shown as a reference that can no longer be opened.
        await dbContext.NoteReferences
            .Where(reference => reference.TargetNoteId == noteId && reference.TargetNote.OwnerUserId == ownerUserId)
            .ExecuteDeleteAsync(cancellationToken);
        // FK_NoteBlocks_Notes_NoteId and FK_NoteReferences_Notes_SourceNoteId cascade: the paragraphs and the note's own
        // references are deleted by SQL Server in the same statement.
        var deleted = await dbContext.Notes
            .Where(note => note.Id == noteId && note.OwnerUserId == ownerUserId)
            .ExecuteDeleteAsync(cancellationToken) > 0;
        await transaction.CommitAsync(cancellationToken);
        return deleted;
    }

    public async Task<NoteCreateStatus> AddAsync(NewNote note, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // The new note goes first on its board, below the smallest order of its context. The lock is held until the
        // commit, so notes created at the same time in a context take one place each.
        var first = await dbContext.Database
            .SqlQuery<int?>($"SELECT MIN([Order]) AS [Value] FROM [dbo].[Notes] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextId] = {note.ContextId}")
            .SingleAsync(cancellationToken);
        var entity = new NoteEntity
        {
            ContextId = note.ContextId,
            OwnerUserId = note.OwnerUserId,
            NoteType = note.NoteType,
            Title = note.Title,
            JournalDate = note.JournalDate,
            Visibility = note.Visibility,
            CreatedByUserId = note.OwnerUserId,
            ModifiedByUserId = note.OwnerUserId,
            Order = (first ?? 1) - 1
        };
        dbContext.Notes.Add(entity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return NoteCreateStatus.Created;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            // The context was deleted after the membership check.
            dbContext.Entry(entity).State = EntityState.Detached;
            return NoteCreateStatus.ContextNotFound;
        }
    }

    public async Task<IReadOnlyList<NoteVersionChange>?> SwapOrderAsync(string ownerUserId, NoteSummary first, NoteSummary second,
        CancellationToken cancellationToken)
    {
        if (!TryReadVersion(first.Version, out var firstVersion) || !TryReadVersion(second.Version, out var secondVersion)) return null;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // One UPDATE exchanges the two orders, only while both notes are still the versions the caller checked.
        // The audit columns stay as they are: a new place on the board is not a change of the note.
        var updated = await dbContext.Notes
            .Where(note => note.OwnerUserId == ownerUserId && note.ArchivedAtUtc == null
                && ((note.Id == first.Id && note.RowVersion == firstVersion) || (note.Id == second.Id && note.RowVersion == secondVersion)))
            .ExecuteUpdateAsync(setters => setters.SetProperty(note => note.Order, note => note.Id == first.Id ? second.Order : first.Order),
                cancellationToken);
        // Disposing the transaction without a commit rolls back a single updated row.
        if (updated != 2) return null;
        var versions = await dbContext.Notes
            .AsNoTracking()
            .Where(note => note.Id == first.Id || note.Id == second.Id)
            .Select(note => new { note.Id, note.RowVersion })
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return versions
            .Select(item => new NoteVersionChange(item.Id, item.Id == first.Id ? first.Version : second.Version, Convert.ToBase64String(item.RowVersion)))
            .ToList();
    }

    public async Task<NoteDocument?> GetDocumentAsync(int noteId, string userId, CancellationToken cancellationToken)
    {
        var document = await VisibleTo(userId)
            .AsNoTracking()
            .Where(note => note.Id == noteId)
            .Select(note => new
            {
                note.Id, note.ContextId, ContextName = note.Context.Name, note.NoteType, note.Title, note.Visibility,
                IsOwner = note.OwnerUserId == userId, note.CreatedAtUtc, note.ModifiedAtUtc, note.RowVersion,
                Blocks = note.NoteBlocks.OrderBy(block => block.Position)
                    .Select(block => new NoteBlockDetails(block.Id, block.Content, block.CreatedAtUtc, block.ModifiedAtUtc))
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (document is null) return null;
        return new NoteDocument(document.Id, document.ContextId, document.ContextName, document.NoteType, document.Title,
            document.Visibility, document.IsOwner, Utc(document.CreatedAtUtc), Utc(document.ModifiedAtUtc), Convert.ToBase64String(document.RowVersion),
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

        // Unchanged text has unchanged references: the stored ones stay as they were at the last save of the text.
        if (!changed)
            return note.RowVersion.AsSpan().SequenceEqual(expectedVersion)
                ? new(NoteSaveStatus.Saved, changes.ExpectedVersion, Audit(saved), Utc(note.ModifiedAtUtc))
                : new(NoteSaveStatus.Conflict);

        await ReplaceReferencesAsync(note.Id, changes.References, changes.SavedAtUtc, cancellationToken);
        note.ModifiedAtUtc = changes.SavedAtUtc;
        note.ModifiedByUserId = changes.OwnerUserId;
        // Always update the note row, even when its audit values are unchanged (two saves in the same second):
        // the UPDATE carries the rowversion check that rejects a save from a stale editor.
        dbContext.Entry(note).Property(item => item.ModifiedAtUtc).IsModified = true;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(NoteSaveStatus.Saved, Convert.ToBase64String(note.RowVersion), Audit(saved), Utc(note.ModifiedAtUtc));
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return new(NoteSaveStatus.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            // A referenced note was deleted after the references were checked: nothing is saved, as for a conflict.
            dbContext.ChangeTracker.Clear();
            return new(NoteSaveStatus.Conflict);
        }
    }

    // At most this many candidates are read for one number; the service keeps those with the number as a whole number.
    private const int MaxReferenceCandidates = 200;

    public async Task<IReadOnlyList<NoteReferenceTarget>> FindReferenceCandidatesAsync(string userId, int contextId, int sourceNoteId,
        string digits, CancellationToken cancellationToken) =>
        await VisibleTo(userId)
            .AsNoTracking()
            .Where(note => note.ContextId == contextId && note.Id != sourceNoteId && note.Title != null && note.Title.Contains(digits))
            .OrderByDescending(note => note.ModifiedAtUtc)
            .ThenByDescending(note => note.Id)
            .Take(MaxReferenceCandidates)
            .Select(note => new NoteReferenceTarget(note.Id, note.Title, note.NoteType))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NoteReferenceTarget>> GetReferenceTargetsAsync(string userId, int contextId, int? sourceNoteId,
        IReadOnlyCollection<int> noteIds, CancellationToken cancellationToken) =>
        await VisibleTo(userId)
            .AsNoTracking()
            .Where(note => note.ContextId == contextId && noteIds.Contains(note.Id) && (sourceNoteId == null || note.Id != sourceNoteId))
            .Select(note => new NoteReferenceTarget(note.Id, note.Title, note.NoteType))
            .ToListAsync(cancellationToken);

    // The note's stored references follow its text: those no longer in it go, new ones get the save time, the others
    // keep theirs. Saved together with the note, under the same version check.
    private async Task ReplaceReferencesAsync(int noteId, IReadOnlyList<NoteReferenceInput> references, DateTime savedAtUtc,
        CancellationToken cancellationToken)
    {
        var wanted = references.Select(reference => (reference.TargetNoteId, reference.Text)).ToHashSet();
        foreach (var stored in await dbContext.NoteReferences.Where(reference => reference.SourceNoteId == noteId).ToListAsync(cancellationToken))
        {
            if (!wanted.Remove((stored.TargetNoteId, stored.DisplayText))) dbContext.NoteReferences.Remove(stored);
        }
        foreach (var (targetNoteId, text) in wanted)
        {
            dbContext.NoteReferences.Add(new NoteReferenceEntity
            {
                SourceNoteId = noteId, TargetNoteId = targetNoteId, DisplayText = text, CreatedAtUtc = savedAtUtc
            });
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
            Order = note.Order,
            RowVersion = note.RowVersion,
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
            row.Visibility, Utc(row.CreatedAtUtc), row.ModifiedAtUtc > row.CreatedAtUtc ? Utc(row.ModifiedAtUtc) : null, row.IsOwner,
            row.Order, Convert.ToBase64String(row.RowVersion));

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
        public int Order { get; init; }
        public byte[] RowVersion { get; init; } = [];
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
