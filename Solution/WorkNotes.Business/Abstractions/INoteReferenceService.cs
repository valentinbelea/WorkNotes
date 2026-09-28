using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// Internal references between notes: the CRs and bugs written in the paragraphs (NoteReferenceRules), each linked to the
// notes whose title names it. The links are stored per paragraph and reference (dbo.NoteReferences), with the notes each
// opens (dbo.NoteReferenceTargets), and follow whatever can change them: a paragraph's text (the note's save stores them
// with it), a note's title, a note created or deleted (the deletion removes the rows that open it).
// A reference opens every note of the board the paragraph's owner may see that has it in its title, except the
// paragraph's own note: one, two or more. With no such note the text stays plain.
public interface INoteReferenceService
{
    // The references a save of a note of ownerUserId stores with its paragraphs, and the notes they open (never the note
    // itself).
    Task<NoteReferenceResolution> ResolveAsync(string ownerUserId, int contextId, int noteId,
        IReadOnlyList<NoteBlockInput> paragraphs, CancellationToken cancellationToken);
    // After a note of the context got a new title (null: none) or was created (previousTitle null): the stored references
    // of the context's paragraphs are resolved again, for the references one of the titles has and the other has not.
    Task RefreshAsync(int contextId, string? previousTitle, string? title, CancellationToken cancellationToken);
    // The document, as the user may see it, with its links: in each paragraph, every place that writes a stored reference
    // with a note the user may see; the link opens those of its notes. References lists those notes.
    Task<NoteDocument> WithLinksAsync(NoteDocument document, string userId, CancellationToken cancellationToken);
}
