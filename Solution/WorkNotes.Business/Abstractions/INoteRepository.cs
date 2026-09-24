using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface INoteRepository
{
    // Notes of one context that the user may see: their own and those shared with the context. Empty for non-members.
    Task<IReadOnlyList<NoteSummary>> GetBoardAsync(string userId, int contextId, CancellationToken cancellationToken);
    Task<NoteCreateStatus> AddAsync(NewNote note, CancellationToken cancellationToken);
}
