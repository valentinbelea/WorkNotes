using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class NoteService(INoteRepository notes, IWorkContextRepository contexts, INoteReferenceService references, TimeProvider time)
    : INoteService
{
    public async Task<IReadOnlyList<NoteMonthGroup>> GetBoardAsync(string userId, int contextId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var board = await notes.GetBoardAsync(userId, contextId, cancellationToken);
        // Grouped by the last change, ISNULL(modified, created): a note changed this month moves to it.
        // Months follow the application's local calendar, like the journal date. Within a month the order the owner
        // arranged comes first (smaller first); notes with the same order show the latest change first, then the
        // latest created, then the highest id, so the result is always the same.
        return board
            .GroupBy(note => LocalMonth(note.LastChangedAtUtc))
            .OrderByDescending(group => group.Key.Year).ThenByDescending(group => group.Key.Month)
            .Select(group => new NoteMonthGroup(group.Key.Year, group.Key.Month, group
                .OrderBy(note => note.Order)
                .ThenByDescending(note => note.LastChangedAtUtc)
                .ThenByDescending(note => note.CreatedAtUtc)
                .ThenByDescending(note => note.Id)
                .ToList()))
            .ToList();
    }

    public async Task<NoteCreateStatus> CreateAsync(string userId, int contextId, string noteType, string? title, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!NoteTypes.IsValid(noteType)) return NoteCreateStatus.InvalidType;
        if (!NoteRules.ValidTitle(title)) return NoteCreateStatus.InvalidTitle;
        // Any member may write notes in the context; the notes themselves stay private to their owner.
        if (await contexts.GetByIdAsync(contextId, userId, cancellationToken) is null) return NoteCreateStatus.ContextNotFound;

        // The journal is dated with the application's local calendar day.
        DateOnly? journalDate = noteType == NoteTypes.Journal ? DateOnly.FromDateTime(time.GetLocalNow().DateTime) : null;
        var normalized = NoteRules.NormalizeTitle(title);
        var status = await notes.AddAsync(new NewNote(contextId, userId, noteType, normalized, journalDate, NoteVisibilities.Private),
            cancellationToken);
        // The references its title names now open it too.
        if (status == NoteCreateStatus.Created) await references.RefreshAsync(contextId, null, normalized, cancellationToken);
        return status;
    }

    public (int Year, int Month) GetCurrentMonth()
    {
        var now = time.GetLocalNow();
        return (now.Year, now.Month);
    }

    public async Task<NoteDocument?> GetDocumentAsync(int noteId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var document = await notes.GetDocumentAsync(noteId, userId, cancellationToken);
        if (document is null) return null;
        // Paragraphs stored before the save normalized line endings (or written by other tools) may hold \r\n. The editor
        // counts a line break as one character, so the text is read as a save stores it and the links are found in it.
        document = document with
        {
            Blocks = document.Blocks.Select(block => block with { Content = NoteRules.NormalizeBlockContent(block.Content) }).ToList()
        };
        return await references.WithLinksAsync(document, userId, cancellationToken);
    }

    public async Task<NoteSaveResult> SaveAsync(string userId, int noteId, string expectedVersion, string? title, string? noteType,
        IReadOnlyList<NoteBlockInput> blocks, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!NoteRules.ValidTitle(title)) return new(NoteSaveStatus.InvalidTitle);
        if (noteType is not null && !NoteTypes.IsValid(noteType)) return new(NoteSaveStatus.InvalidType);
        if (Normalize(blocks) is not { } paragraphs) return new(NoteSaveStatus.InvalidContent);

        // The note as its card showed it: the paragraphs themselves are read by the repository's save.
        var note = await notes.GetSummaryAsync(noteId, userId, cancellationToken);
        if (note is null) return new(NoteSaveStatus.NotFound);
        // Members may read a shared note; only its owner edits it.
        if (!note.IsOwner) return new(NoteSaveStatus.Forbidden);
        if (string.IsNullOrWhiteSpace(expectedVersion)) return new(NoteSaveStatus.Conflict);

        // A note that becomes a journal is dated with the local day it was created (the day a journal created then would
        // have); an article has no date. A note that keeps its type keeps its date.
        var type = noteType ?? note.NoteType;
        var journalDate = type == note.NoteType ? note.JournalDate : type == NoteTypes.Journal ? LocalDay(note.CreatedAtUtc) : null;

        // The references of the paragraphs are stored with them, in the same transaction.
        var normalized = NoteRules.NormalizeTitle(title);
        var resolution = await references.ResolveAsync(userId, note.ContextId, noteId, paragraphs, cancellationToken);
        var result = await notes.SaveAsync(
            new NoteChanges(noteId, userId, expectedVersion, normalized, type, journalDate, paragraphs, resolution.References, SavedAtUtc()),
            cancellationToken);
        if (result.Status != NoteSaveStatus.Saved) return result;
        // Other paragraphs of the board may name the note by its old or its new title.
        if (normalized != note.Title) await references.RefreshAsync(note.ContextId, note.Title, normalized, cancellationToken);
        return await WithCardAsync(WithLinks(result, resolution), userId, note, note with { Title = normalized, NoteType = type, JournalDate = journalDate },
            paragraphs, cancellationToken);
    }

    public async Task<NoteReferenceLookup> LookUpReferenceAsync(string userId, int noteId, string? text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var note = await notes.GetSummaryAsync(noteId, userId, cancellationToken);
        if (note is null) return new(NoteReferenceLookupStatus.NotFound);
        // Members may read a shared note; only its owner writes in it.
        if (!note.IsOwner) return new(NoteReferenceLookupStatus.Forbidden);
        return await references.LookUpAsync(userId, note.ContextId, noteId, text, cancellationToken);
    }

    public Task<NoteSummary?> GetSummaryAsync(int noteId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        return notes.GetSummaryAsync(noteId, userId, cancellationToken);
    }

    public async Task<NoteRenameResult> RenameAsync(string userId, int noteId, string? title, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!NoteRules.ValidTitle(title)) return new(NoteSaveStatus.InvalidTitle);
        var note = await notes.GetSummaryAsync(noteId, userId, cancellationToken);
        if (note is null) return new(NoteSaveStatus.NotFound);
        if (!note.IsOwner) return new(NoteSaveStatus.Forbidden);
        var normalized = NoteRules.NormalizeTitle(title);
        var savedAtUtc = SavedAtUtc();
        if (!await notes.RenameAsync(noteId, userId, normalized, savedAtUtc, cancellationToken)) return new(NoteSaveStatus.NotFound);
        // The references of the board that name the note by its old or its new title follow.
        await references.RefreshAsync(note.ContextId, note.Title, normalized, cancellationToken);
        return new(NoteSaveStatus.Saved, note with { Title = normalized, ModifiedAtUtc = savedAtUtc });
    }

    public async Task<NoteDeleteStatus> DeleteAsync(string userId, int noteId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var note = await notes.GetSummaryAsync(noteId, userId, cancellationToken);
        if (note is null) return NoteDeleteStatus.NotFound;
        if (!note.IsOwner) return NoteDeleteStatus.Forbidden;
        // The references of the board that opened it no longer do (the deletion takes those rows away): nothing else to
        // resolve, since a note deleted only takes a target away.
        return await notes.DeleteAsync(noteId, userId, cancellationToken) ? NoteDeleteStatus.Deleted : NoteDeleteStatus.NotFound;
    }

    public async Task<NoteOrderResult> SwapOrderAsync(string userId, int noteId, int targetNoteId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (noteId == targetNoteId) return new(NoteOrderStatus.InvalidTarget);
        var note = await notes.GetSummaryAsync(noteId, userId, cancellationToken);
        var target = await notes.GetSummaryAsync(targetNoteId, userId, cancellationToken);
        if (note is null || target is null) return new(NoteOrderStatus.NotFound);
        // Only the owner changes a note, its place on the board included.
        if (!note.IsOwner || !target.IsOwner) return new(NoteOrderStatus.Forbidden);
        // Notes change places only within their group: the same board and the same month.
        var month = LocalMonth(note.LastChangedAtUtc);
        if (note.ContextId != target.ContextId || month != LocalMonth(target.LastChangedAtUtc)) return new(NoteOrderStatus.InvalidTarget);

        var versions = await notes.SwapOrderAsync(userId, note, target, cancellationToken);
        if (versions is null) return new(NoteOrderStatus.Conflict);
        // The month as the board now shows it, so the page follows the stored order.
        var board = await GetBoardAsync(userId, note.ContextId, cancellationToken);
        var noteIds = board.FirstOrDefault(group => (group.Year, group.Month) == month)?.Notes.Select(item => item.Id).ToList() ?? [];
        return new(NoteOrderStatus.Saved, noteIds, versions);
    }

    // Where each saved paragraph shows its links now, for the editor: those of the references just stored with it, as
    // the resolution found them in its text.
    private static NoteSaveResult WithLinks(NoteSaveResult result, NoteReferenceResolution resolution)
    {
        if (resolution.References.Count == 0 || result.Blocks is null) return result;
        return result with
        {
            Blocks = result.Blocks
                .Select(block => block with { Links = resolution.Links.TryGetValue(block.Id, out var links) ? links : [] })
                .ToList(),
            References = resolution.Targets
        };
    }

    // The saved note as its card on the board shows it now, from what was just stored (the board would read the same),
    // so the board behind the editor follows the save without being read again: note is the card as it was, stored the
    // same card with the title and type just saved. Only a save that moved the note to another month (a change made in
    // the current month) reads the board, for that month's order; within a month a note keeps its place (its Order).
    private async Task<NoteSaveResult> WithCardAsync(NoteSaveResult result, string userId, NoteSummary note, NoteSummary stored,
        IReadOnlyList<NoteBlockInput> paragraphs, CancellationToken cancellationToken)
    {
        var saved = stored with
        {
            Preview = NoteRules.PreviewOf(paragraphs.Select(paragraph => paragraph.Content)),
            // As on the board: the note counts as changed once its last change is later than its creation.
            ModifiedAtUtc = result.ModifiedAtUtc > note.CreatedAtUtc ? result.ModifiedAtUtc : note.ModifiedAtUtc,
            Version = result.Version ?? note.Version
        };
        var month = LocalMonth(saved.LastChangedAtUtc);
        if (month == LocalMonth(note.LastChangedAtUtc)) return result with { Note = saved };
        var board = await GetBoardAsync(userId, note.ContextId, cancellationToken);
        return result with { Note = saved, Month = board.FirstOrDefault(group => (group.Year, group.Month) == month) };
    }

    // Audit times are kept to the second, the precision of the stored columns, so they read the same after a reload.
    private DateTime SavedAtUtc()
    {
        var now = time.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }

    // Null when the paragraphs cannot be stored as sent: empty or duplicate ids, invalid text or too much content.
    private static IReadOnlyList<NoteBlockInput>? Normalize(IReadOnlyList<NoteBlockInput>? blocks)
    {
        if (blocks is null || blocks.Count > NoteRules.MaxBlocks) return null;
        var ids = new HashSet<Guid>();
        var result = new List<NoteBlockInput>(blocks.Count);
        var length = 0;
        foreach (var block in blocks)
        {
            if (block.Id == Guid.Empty || !ids.Add(block.Id)) return null;
            var content = block.Content is null ? null : NoteRules.NormalizeBlockContent(block.Content);
            if (!NoteRules.ValidBlockContent(content)) return null;
            length += content!.Length;
            if (length > NoteRules.MaxContentLength) return null;
            result.Add(block with { Content = content });
        }
        return result;
    }

    private (int Year, int Month) LocalMonth(DateTime utc)
    {
        var local = LocalTime(utc);
        return (local.Year, local.Month);
    }

    private DateOnly LocalDay(DateTime utc) => DateOnly.FromDateTime(LocalTime(utc));

    private DateTime LocalTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), time.LocalTimeZone);
}
