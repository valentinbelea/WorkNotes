namespace WorkNotes.Business.Models;

// A stored reference of a paragraph with one of the notes it opens, as a reader may follow it: the normalized reference
// and the note.
public sealed record NoteBlockTarget(Guid NoteBlockId, string NormalizedReference, NoteReferenceTarget Target);
