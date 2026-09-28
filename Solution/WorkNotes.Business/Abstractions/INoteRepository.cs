using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface INoteRepository
{
    // Notes of one context that the user may see: their own and those shared with the context. Empty for non-members.
    Task<IReadOnlyList<NoteSummary>> GetBoardAsync(string userId, int contextId, CancellationToken cancellationToken);
    // The new note goes first on its board: its order is below that of every note of its context.
    Task<NoteCreateStatus> AddAsync(NewNote note, CancellationToken cancellationToken);
    // The note with its paragraphs in order, when the user may see it (same rule as the board).
    Task<NoteDocument?> GetDocumentAsync(int noteId, string userId, CancellationToken cancellationToken);
    // Applies the changes to a note owned by changes.OwnerUserId: kept paragraphs keep id and creation audit,
    // changed ones get a new modification audit, missing ones are removed with their stored references. In the same
    // transaction the stored references of its paragraphs become changes.References, each with its notes: those no longer
    // there go, new ones are added, the others stay (with their creation time). Conflict when the version is stale, or
    // when a note a reference opens was deleted meanwhile (nothing is saved).
    Task<NoteSaveResult> SaveAsync(NoteChanges changes, CancellationToken cancellationToken);
    // One card of the board, when the user may see the note (same rule as the board).
    Task<NoteSummary?> GetSummaryAsync(int noteId, string userId, CancellationToken cancellationToken);
    // Changes the title of a note owned by ownerUserId and audits the change; false when there is no such note.
    Task<bool> RenameAsync(int noteId, string ownerUserId, string? title, DateTime savedAtUtc, CancellationToken cancellationToken);
    // Deletes a note owned by ownerUserId together with its paragraphs and their stored references; the stored references
    // of other paragraphs no longer open it, and those it was the only note of go (their text stays as it is). False when
    // there is no such note.
    Task<bool> DeleteAsync(int noteId, string ownerUserId, CancellationToken cancellationToken);
    // Exchanges the orders of two notes owned by ownerUserId while both are still at the versions they were read with;
    // null, with nothing saved, when either changed in the meantime. Otherwise each note's version before and after.
    Task<IReadOnlyList<NoteVersionChange>?> SwapOrderAsync(string ownerUserId, NoteSummary first, NoteSummary second,
        CancellationToken cancellationToken);
}
