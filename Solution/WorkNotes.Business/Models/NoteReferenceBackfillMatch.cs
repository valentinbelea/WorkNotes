namespace WorkNotes.Business.Models;

// A number of a note that other notes of its board have whole in their title, Count times in its text. With a single
// target it becomes a reference to it; with several it stays as it is, for the owner to choose in the editor.
public sealed record NoteReferenceBackfillMatch(string Number, IReadOnlyList<NoteReferenceTarget> Targets, int Count);
