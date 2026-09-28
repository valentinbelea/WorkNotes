using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// Internal references between notes: the CRs and bugs written in the paragraphs (NoteReferenceRules), each linked to the
// note whose title names it. The links are stored per paragraph and reference (dbo.NoteReferences) and follow whatever
// can change them: a paragraph's text (the note's save stores them with it), a note's title, a note created or deleted.
// A reference opens a note when exactly one note of the board the paragraph's owner may see has it in its title, and that
// note is another one: with no such note, or with several, the text stays plain.
public interface INoteReferenceService
{
    // The references a save of a note of ownerUserId stores with its paragraphs, and the notes they open. title is the
    // title saved with them: the note counts among the notes with the reference in their title.
    Task<NoteReferenceResolution> ResolveAsync(string ownerUserId, int contextId, int noteId, string? title,
        IReadOnlyList<NoteBlockInput> paragraphs, CancellationToken cancellationToken);
    // After a note of the context got a new title, was created (previousTitle null) or was deleted (title null): the stored
    // references of the context's paragraphs are resolved again, for the references one of the titles has and the other
    // has not.
    Task RefreshAsync(int contextId, string? previousTitle, string? title, CancellationToken cancellationToken);
    // The document, as the user may see it, with its links: in each paragraph, every place that writes a stored reference
    // whose note the user may see. References lists those notes.
    Task<NoteDocument> WithLinksAsync(NoteDocument document, string userId, CancellationToken cancellationToken);
}
