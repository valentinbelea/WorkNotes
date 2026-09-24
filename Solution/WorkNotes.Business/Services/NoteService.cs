using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class NoteService(INoteRepository notes, IWorkContextRepository contexts, TimeProvider time) : INoteService
{
    public async Task<IReadOnlyList<NoteMonthGroup>> GetBoardAsync(string userId, int contextId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var board = await notes.GetBoardAsync(userId, contextId, cancellationToken);
        // Months follow the application's local calendar, like the journal date.
        return board
            .GroupBy(note => LocalMonth(note.CreatedAtUtc))
            .OrderByDescending(group => group.Key.Year).ThenByDescending(group => group.Key.Month)
            .Select(group => new NoteMonthGroup(group.Key.Year, group.Key.Month, group
                .OrderBy(note => note.NoteType == NoteTypes.Journal ? 0 : 1)
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

    public Task<NoteDocument?> GetDocumentAsync(int noteId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        return notes.GetDocumentAsync(noteId, userId, cancellationToken);
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

        return await notes.SaveAsync(
            new NoteChanges(noteId, userId, expectedVersion, NoteRules.NormalizeTitle(title), paragraphs, time.GetUtcNow().UtcDateTime),
            cancellationToken);
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

    private (int Year, int Month) LocalMonth(DateTime createdAtUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc), time.LocalTimeZone);
        return (local.Year, local.Month);
    }
}
