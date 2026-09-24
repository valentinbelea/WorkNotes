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

    private (int Year, int Month) LocalMonth(DateTime createdAtUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc), time.LocalTimeZone);
        return (local.Year, local.Month);
    }
}
