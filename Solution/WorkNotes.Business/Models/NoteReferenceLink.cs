namespace WorkNotes.Business.Models;

// Where a paragraph shows a link: its text from Start, Length characters, opens the notes TargetNoteIds (one or more, in
// the order of their ids).
public sealed record NoteReferenceLink(int Start, int Length, IReadOnlyList<int> TargetNoteIds);
