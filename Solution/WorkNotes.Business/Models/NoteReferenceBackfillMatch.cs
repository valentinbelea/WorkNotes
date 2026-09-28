namespace WorkNotes.Business.Models;

// A number of a note that other notes of its board have whole in their title (Candidates), Count times in its text.
// Target is the note it becomes a reference to: the only candidate or, among several, the only article (a CR or a bug is
// documented in an article, which journals mention); null when the choice is left to the owner, in the editor.
public sealed record NoteReferenceBackfillMatch(string Number, IReadOnlyList<NoteReferenceTarget> Candidates, NoteReferenceTarget? Target,
    int Count);
