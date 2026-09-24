using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface INoteRepository
{
    // Notes of one context that the user may see: their own and those shared with the context. Empty for non-members.
    Task<IReadOnlyList<NoteSummary>> GetBoardAsync(string userId, int contextId, CancellationToken cancellationToken);
    Task<NoteCreateStatus> AddAsync(NewNote note, CancellationToken cancellationToken);
    // The note with its paragraphs in order, when the user may see it (same rule as the board).
    Task<NoteDocument?> GetDocumentAsync(int noteId, string userId, CancellationToken cancellationToken);
    // Applies the changes to a note owned by changes.OwnerUserId: kept paragraphs keep id and creation audit,
    // changed ones get a new modification audit, missing ones are removed. Conflict when the version is stale.
    Task<NoteSaveResult> SaveAsync(NoteChanges changes, CancellationToken cancellationToken);
}
