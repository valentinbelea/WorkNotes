using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface INoteRepository
{
    // Notes the user may see: their own and those shared with a context they belong to, newest first.
    Task<IReadOnlyList<NoteSummary>> GetBoardAsync(string userId, CancellationToken cancellationToken);
    // Returns JournalExists when the daily journal for that owner, context and date is already stored.
    Task<NoteCreateStatus> AddAsync(NewNote note, CancellationToken cancellationToken);
}
