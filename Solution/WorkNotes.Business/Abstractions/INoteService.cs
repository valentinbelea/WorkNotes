using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface INoteService
{
    // The board of one context: months newest first; in each month the notes by Order (smaller first), then by
    // last change, creation and id, newest first.
    Task<IReadOnlyList<NoteMonthGroup>> GetBoardAsync(string userId, int contextId, CancellationToken cancellationToken);
    // Creates a Private note in a context the user belongs to; a Journal is dated today. Several journals per day are allowed.
    // The references its title names may now open it (INoteReferenceService.RefreshAsync).
    Task<NoteCreateStatus> CreateAsync(string userId, int contextId, string noteType, string? title, CancellationToken cancellationToken);
    // The month a note created now belongs to, so the board can place a new card in the right group.
    (int Year, int Month) GetCurrentMonth();
    // Null when the note does not exist or the user may not see it. The paragraphs have their links for the user.
    Task<NoteDocument?> GetDocumentAsync(int noteId, string userId, CancellationToken cancellationToken);
    // Only the owner saves; the paragraphs are the whole content of the note, in document order. Their references are
    // stored with them; a new title also updates the references of the board that name the note. The type (Journal or
    // Article; null keeps it) may change: a note that becomes a journal is dated with the local day it was created, an
    // article has no date. The result has the note as its board card now shows it and, when the save moved it to another
    // month, that month in its board order.
    Task<NoteSaveResult> SaveAsync(string userId, int noteId, string expectedVersion, string? title, string? noteType,
        IReadOnlyList<NoteBlockInput> blocks, CancellationToken cancellationToken);
    // Only the owner, who writes the note: the reference the text ends with (the text of a paragraph up to where the owner
    // is typing) and the notes it would open (INoteReferenceService.LookUpAsync).
    Task<NoteReferenceLookup> LookUpReferenceAsync(string userId, int noteId, string? text, CancellationToken cancellationToken);
    // Null when the note does not exist or the user may not see it.
    Task<NoteSummary?> GetSummaryAsync(int noteId, string userId, CancellationToken cancellationToken);
    // Only the owner renames a note, from its card; an empty title makes it untitled. The references of the board that
    // name the note by its old or its new title follow.
    Task<NoteRenameResult> RenameAsync(string userId, int noteId, string? title, CancellationToken cancellationToken);
    // Only the owner deletes a note; its paragraphs are deleted with it. The references of the board no longer open it.
    Task<NoteDeleteStatus> DeleteAsync(string userId, int noteId, CancellationToken cancellationToken);
    // The owner swaps two of their notes of the same board and month: each takes the other's place.
    // The result has the month's notes in their new order.
    Task<NoteOrderResult> SwapOrderAsync(string userId, int noteId, int targetNoteId, CancellationToken cancellationToken);
}
