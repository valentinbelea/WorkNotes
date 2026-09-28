using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// Data access for the stored references between notes (dbo.NoteReferences, with the notes each opens in
// dbo.NoteReferenceTargets) and for what they are resolved from.
public interface INoteReferenceRepository
{
    // Notes of the context the user may see (same rule as the board) whose title contains the digits of one of the
    // numbers: the notes the user's references there may open. Only a filter: the service reads the references in each
    // title. Empty for a non-member.
    Task<IReadOnlyList<NoteReferenceTarget>> GetCandidatesAsync(string userId, int contextId, IReadOnlyCollection<long> numbers,
        CancellationToken cancellationToken);
    // The paragraphs of the context that can have stored references, whose text contains the digits of one of the
    // numbers (a filter as well): those of notes that are not archived, whose owner still belongs to the context (the
    // board's rule for each note's own owner).
    Task<IReadOnlyList<NoteReferenceSource>> GetSourcesAsync(int contextId, IReadOnlyCollection<long> numbers,
        CancellationToken cancellationToken);
    // In one transaction, for each of the paragraphs that is still at the version it was read with: its stored references
    // to the normalized references given become those of references, each with its notes (a reference or a note it
    // keeps keeps its creation time; new ones get savedAtUtc). A paragraph changed or deleted since it was read is left as
    // it is: its save stored its references. False, with nothing saved, when a note one of the references opens was
    // deleted meanwhile.
    Task<bool> ReplaceReferencesAsync(IReadOnlyCollection<string> normalizedReferences, IReadOnlyList<NoteReferenceSource> paragraphs,
        IReadOnlyList<NoteBlockReference> references, DateTime savedAtUtc, CancellationToken cancellationToken);
    // The stored references of the paragraphs of a note the user may see, one for each of their notes the user may see
    // too (same rule as the board), with that note. Empty when the user may not see the note.
    Task<IReadOnlyList<NoteBlockTarget>> GetTargetsAsync(int noteId, string userId, CancellationToken cancellationToken);
}
