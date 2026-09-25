using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class NoteService(INoteRepository notes, IWorkContextRepository contexts, TimeProvider time) : INoteService
{
    public async Task<IReadOnlyList<NoteMonthGroup>> GetBoardAsync(string userId, int contextId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var board = await WithPreviewReferencesAsync(userId, contextId, await notes.GetBoardAsync(userId, contextId, cancellationToken),
            cancellationToken);
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
        return await notes.AddAsync(
            new NewNote(contextId, userId, noteType, NoteRules.NormalizeTitle(title), journalDate, NoteVisibilities.Private),
            cancellationToken);
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
        var targetIds = ReferencedIds(document.Blocks.Select(block => block.Content));
        return targetIds.Count == 0
            ? document
            : document with { References = await notes.GetReferenceTargetsAsync(userId, document.ContextId, document.Id, targetIds, cancellationToken) };
    }

    public async Task<NoteSaveResult> SaveAsync(string userId, int noteId, string expectedVersion, string? title,
        IReadOnlyList<NoteBlockInput> blocks, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!NoteRules.ValidTitle(title)) return new(NoteSaveStatus.InvalidTitle);
        if (Normalize(blocks) is not { } paragraphs) return new(NoteSaveStatus.InvalidContent);

        var document = await notes.GetDocumentAsync(noteId, userId, cancellationToken);
        if (document is null) return new(NoteSaveStatus.NotFound);
        // Members may read a shared note; only its owner edits it.
        if (!document.IsOwner) return new(NoteSaveStatus.Forbidden);
        if (string.IsNullOrWhiteSpace(expectedVersion)) return new(NoteSaveStatus.Conflict);

        // The text keeps every reference as it was written; only those whose target the owner may open are stored.
        var mentioned = NoteReferenceRules.Find(paragraphs.Select(paragraph => paragraph.Content));
        IReadOnlyList<NoteReferenceInput> references = [];
        if (mentioned.Count > 0)
        {
            var targets = await notes.GetReferenceTargetsAsync(userId, document.ContextId, document.Id,
                mentioned.Select(reference => reference.TargetNoteId).Distinct().ToList(), cancellationToken);
            var valid = targets.Select(target => target.Id).ToHashSet();
            references = mentioned.Where(reference => valid.Contains(reference.TargetNoteId)).ToList();
        }
        return await notes.SaveAsync(
            new NoteChanges(noteId, userId, expectedVersion, NoteRules.NormalizeTitle(title), paragraphs, references, SavedAtUtc()),
            cancellationToken);
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
        return await notes.RenameAsync(noteId, userId, normalized, savedAtUtc, cancellationToken)
            ? new(NoteSaveStatus.Saved, note with { Title = normalized, ModifiedAtUtc = savedAtUtc })
            : new(NoteSaveStatus.NotFound);
    }

    public async Task<NoteDeleteStatus> DeleteAsync(string userId, int noteId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var note = await notes.GetSummaryAsync(noteId, userId, cancellationToken);
        if (note is null) return NoteDeleteStatus.NotFound;
        if (!note.IsOwner) return NoteDeleteStatus.Forbidden;
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

    public async Task<IReadOnlyList<NoteReferenceTarget>?> FindReferenceTargetsAsync(string userId, int noteId, string? number,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var note = await notes.GetSummaryAsync(noteId, userId, cancellationToken);
        if (note is null) return null;
        // Only the owner writes in the note, so only the owner is offered references.
        if (!note.IsOwner || !NoteReferenceRules.IsReferenceNumber(number)) return [];
        var candidates = await notes.FindReferenceCandidatesAsync(userId, note.ContextId, note.Id, number!, cancellationToken);
        // The data access matches the digits anywhere in the title; the number must be whole (not part of 130080).
        return candidates.Where(candidate => NoteReferenceRules.TitleContainsNumber(candidate.Title, number!)).ToList();
    }

    public async Task<IReadOnlyList<NoteReferenceTarget>?> GetReferenceTargetsAsync(string userId, int noteId,
        IReadOnlyCollection<int> targetNoteIds, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(targetNoteIds);
        cancellationToken.ThrowIfCancellationRequested();
        var note = await notes.GetSummaryAsync(noteId, userId, cancellationToken);
        if (note is null) return null;
        var ids = targetNoteIds.Where(id => id > 0 && id != note.Id).Distinct().Take(NoteReferenceRules.MaxTargetsPerRequest).ToList();
        return ids.Count == 0 ? [] : await notes.GetReferenceTargetsAsync(userId, note.ContextId, note.Id, ids, cancellationToken);
    }

    // Each card gets the notes the references in its preview can open: other notes of the board the user may see.
    private async Task<IReadOnlyList<NoteSummary>> WithPreviewReferencesAsync(string userId, int contextId, IReadOnlyList<NoteSummary> board,
        CancellationToken cancellationToken)
    {
        var referenced = board.Select(note => (Note: note, TargetIds: ReferencedIds([note.Preview]))).ToList();
        var targetIds = referenced.SelectMany(item => item.TargetIds).Distinct().ToList();
        if (targetIds.Count == 0) return board;
        var targets = (await notes.GetReferenceTargetsAsync(userId, contextId, null, targetIds, cancellationToken)).ToDictionary(target => target.Id);
        return referenced
            .Select(item => item.TargetIds.Count == 0 ? item.Note : item.Note with
            {
                References = item.TargetIds.Where(id => id != item.Note.Id && targets.ContainsKey(id)).Select(id => targets[id]).ToList()
            })
            .ToList();
    }

    private static List<int> ReferencedIds(IEnumerable<string?> texts) =>
        texts.SelectMany(text => NoteReferenceRules.Split(text))
            .Select(part => part.TargetNoteId)
            .OfType<int>()
            .Distinct()
            .ToList();

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
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), time.LocalTimeZone);
        return (local.Year, local.Month);
    }
}
