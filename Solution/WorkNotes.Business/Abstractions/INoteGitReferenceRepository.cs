using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// The branches linked to the references of the paragraphs (dbo.NoteBlockGitReferences, with the branches in
// dbo.GitReferences and the references in dbo.WorkReferences).
public interface INoteGitReferenceRepository
{
    // Whether the user owns the note; null when the note does not exist or the user may not see it (same rule as the board).
    Task<bool?> IsOwnerAsync(int noteId, string userId, CancellationToken cancellationToken);

    // The links of the paragraphs of a note the user may see (same rule as the board), each with its paragraph, in the
    // order of the paragraphs and then of the links. Empty when the user may not see the note.
    Task<IReadOnlyList<StoredNoteGitReference>> GetAsync(int noteId, string userId, CancellationToken cancellationToken);

    // The text of a paragraph of a note owned by ownerUserId; null when the note is not theirs or has no such paragraph.
    Task<string?> GetBlockContentAsync(int noteId, string ownerUserId, Guid blockId, CancellationToken cancellationToken);

    // Links the branch to the reference of the paragraph, which must belong to a note of reference.CreatedByUserId. The
    // branch is added to the catalog when it is not there (a known one takes the repository's current name and address),
    // and so is the reference; a link the paragraph has already is left as it is. False when the paragraph is gone.
    Task<bool> AddAsync(int noteId, NewNoteGitReference reference, CancellationToken cancellationToken);

    // Removes a link of a note owned by ownerUserId (the branch stays in the catalog); false when there is no such link.
    Task<bool> RemoveAsync(int noteId, string ownerUserId, int linkId, CancellationToken cancellationToken);
}
