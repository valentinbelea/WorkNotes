using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// Data access for creating the references of the existing notes (INoteReferenceBackfillService).
public interface INoteReferenceBackfillRepository
{
    // Every note as its owner sees it on the board, by board, owner and id: the owner's own notes that are not archived,
    // in a context the owner still belongs to (the board's rule, applied for each note's owner).
    Task<IReadOnlyList<NoteReferenceSource>> GetReferenceSourcesAsync(CancellationToken cancellationToken);
    // Every note of the context the user may see (same rule as the board), with a title or without: the notes the
    // user's references there can open.
    Task<IReadOnlyList<NoteReferenceTarget>> GetAllReferenceTargetsAsync(string userId, int contextId, CancellationToken cancellationToken);
    // The note with its paragraphs in order, when the user may see it (same as INoteRepository.GetDocumentAsync); null
    // when it does not exist or the user may not see it.
    Task<NoteDocument?> GetDocumentAsync(int noteId, string userId, CancellationToken cancellationToken);
    // Gives the paragraphs of a note owned by ownerUserId their new text and makes the note's stored references these
    // (kept ones keep their creation time), while the note is still at expectedVersion and not archived; false, with
    // nothing saved, otherwise. Nothing else changes: not the note's version, audit or place on the board, nor the
    // paragraphs' audit.
    Task<bool> SaveReferencesAsync(int noteId, string ownerUserId, string expectedVersion, IReadOnlyList<NoteBlockInput> paragraphs,
        IReadOnlyList<NoteReferenceInput> references, DateTime savedAtUtc, CancellationToken cancellationToken);
}
