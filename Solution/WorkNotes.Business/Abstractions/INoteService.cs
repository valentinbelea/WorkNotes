using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface INoteService
{
    // The board of one context: months newest first; in each month the notes by Order (smaller first), then by
    // last change, creation and id, newest first. Each card has the notes the references in its preview can open.
    Task<IReadOnlyList<NoteMonthGroup>> GetBoardAsync(string userId, int contextId, CancellationToken cancellationToken);
    // Creates a Private note in a context the user belongs to; a Journal is dated today. Several journals per day are allowed.
    Task<NoteCreateStatus> CreateAsync(string userId, int contextId, string noteType, string? title, CancellationToken cancellationToken);
    // The month a note created now belongs to, so the board can place a new card in the right group.
    (int Year, int Month) GetCurrentMonth();
    // Null when the note does not exist or the user may not see it. The document has the notes its references can open.
    Task<NoteDocument?> GetDocumentAsync(int noteId, string userId, CancellationToken cancellationToken);
    // Only the owner saves; the paragraphs are the whole content of the note, in document order. The references in the
    // text are stored with it when their target is another note of the board the owner may see.
    Task<NoteSaveResult> SaveAsync(string userId, int noteId, string expectedVersion, string? title, IReadOnlyList<NoteBlockInput> blocks, CancellationToken cancellationToken);
    // Null when the note does not exist or the user may not see it.
    Task<NoteSummary?> GetSummaryAsync(int noteId, string userId, CancellationToken cancellationToken);
    // Only the owner renames a note, from its card; an empty title makes it untitled.
    Task<NoteRenameResult> RenameAsync(string userId, int noteId, string? title, CancellationToken cancellationToken);
    // Only the owner deletes a note; its paragraphs are deleted with it.
    Task<NoteDeleteStatus> DeleteAsync(string userId, int noteId, CancellationToken cancellationToken);
    // The owner swaps two of their notes of the same board and month: each takes the other's place.
    // The result has the month's notes in their new order.
    Task<NoteOrderResult> SwapOrderAsync(string userId, int noteId, int targetNoteId, CancellationToken cancellationToken);
    // The notes a number typed in a note can refer to: other notes of the same board the user may see whose title has
    // the number as a whole number. Null when the note is not visible; empty for another member's note (only its owner
    // writes in it) or a value that is not a reference number.
    Task<IReadOnlyList<NoteReferenceTarget>?> FindReferenceTargetsAsync(string userId, int noteId, string? number,
        CancellationToken cancellationToken);
    // Among targetNoteIds (at most NoteReferenceRules.MaxTargetsPerRequest are read), the notes the references of the
    // note can open for the user. Null when the note is not visible.
    Task<IReadOnlyList<NoteReferenceTarget>?> GetReferenceTargetsAsync(string userId, int noteId, IReadOnlyCollection<int> targetNoteIds,
        CancellationToken cancellationToken);
}
