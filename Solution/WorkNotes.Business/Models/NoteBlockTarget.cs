namespace WorkNotes.Business.Models;

// A stored reference of a paragraph, as a reader may follow it: the normalized reference and the note it opens.
public sealed record NoteBlockTarget(Guid NoteBlockId, string NormalizedReference, NoteReferenceTarget Target);
