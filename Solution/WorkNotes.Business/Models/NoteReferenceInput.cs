namespace WorkNotes.Business.Models;

// A reference in the text of a note: the note it points to and the number shown for it.
public sealed record NoteReferenceInput(int TargetNoteId, string Text);
