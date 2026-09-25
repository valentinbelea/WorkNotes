namespace WorkNotes.Business.Models;

// A piece of a note's text: plain text, or a reference (TargetNoteId set) showing Text, the number it was made from.
public sealed record NoteTextPart(string Text, int? TargetNoteId);
