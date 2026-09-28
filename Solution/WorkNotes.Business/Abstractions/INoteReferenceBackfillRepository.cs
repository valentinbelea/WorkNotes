using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// Data access for creating the references of the existing notes (INoteReferenceBackfillService).
public interface INoteReferenceBackfillRepository
{
    // The notes their owner can edit, by board, owner and id: not archived, in a context the owner still belongs to.
    Task<IReadOnlyList<NoteReferenceSource>> GetReferenceSourcesAsync(CancellationToken cancellationToken);
    // Every note of the context the user may see (same rule as the board), with a title or without: the notes the
    // user's references there can open.
    Task<IReadOnlyList<NoteReferenceTarget>> GetAllReferenceTargetsAsync(string userId, int contextId, CancellationToken cancellationToken);
    // The note with its paragraphs in order, when the user may see it (same as INoteRepository.GetDocumentAsync).
    Task<NoteDocument?> GetDocumentAsync(int noteId, string userId, CancellationToken cancellationToken);
    // Gives the paragraphs their new text and makes the note's stored references these (kept ones keep their creation
    // time), while the note is still at expectedVersion; false, with nothing saved, when it is not. Nothing else
    // changes: not the note's version, audit or place on the board, nor the paragraphs' audit.
    Task<bool> SaveReferencesAsync(int noteId, string expectedVersion, IReadOnlyList<NoteBlockInput> paragraphs,
        IReadOnlyList<NoteReferenceInput> references, DateTime savedAtUtc, CancellationToken cancellationToken);
}
