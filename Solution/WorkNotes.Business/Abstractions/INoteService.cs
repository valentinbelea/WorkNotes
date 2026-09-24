using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface INoteService
{
    // The board of one context: months newest first; in each month journals, then articles, each newest first.
    Task<IReadOnlyList<NoteMonthGroup>> GetBoardAsync(string userId, int contextId, CancellationToken cancellationToken);
    // Creates a Private note in a context the user belongs to; a Journal is dated today. Several journals per day are allowed.
    Task<NoteCreateStatus> CreateAsync(string userId, int contextId, string noteType, string? title, CancellationToken cancellationToken);
    // The month a note created now belongs to, so the board can place a new card in the right group.
    (int Year, int Month) GetCurrentMonth();
}
