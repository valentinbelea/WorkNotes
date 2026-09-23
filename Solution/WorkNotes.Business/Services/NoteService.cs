using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class NoteService(INoteRepository notes, IWorkContextRepository contexts, TimeProvider time) : INoteService
{
    public Task<IReadOnlyList<NoteSummary>> GetBoardAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        return notes.GetBoardAsync(userId, cancellationToken);
    }

    public async Task<NoteCreateStatus> CreateAsync(string userId, int contextId, string noteType, string? title, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!NoteTypes.IsValid(noteType)) return NoteCreateStatus.InvalidType;
        if (!NoteRules.ValidTitle(title)) return NoteCreateStatus.InvalidTitle;
        // Any member may write notes in the context; the notes themselves stay private to their owner.
        if (await contexts.GetByIdAsync(contextId, userId, cancellationToken) is null) return NoteCreateStatus.ContextNotFound;

        // The daily journal is dated with the application's local calendar day.
        DateOnly? journalDate = noteType == NoteTypes.Journal ? DateOnly.FromDateTime(time.GetLocalNow().DateTime) : null;
        return await notes.AddAsync(
            new NewNote(contextId, userId, noteType, NoteRules.NormalizeTitle(title), journalDate, NoteVisibilities.Private),
            cancellationToken);
    }
}
