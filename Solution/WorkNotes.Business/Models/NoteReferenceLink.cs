namespace WorkNotes.Business.Models;

// Where a paragraph shows a link: its text from Start, Length characters, opens the note TargetNoteId.
public sealed record NoteReferenceLink(int Start, int Length, int TargetNoteId);
