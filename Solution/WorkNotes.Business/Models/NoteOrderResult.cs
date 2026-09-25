namespace WorkNotes.Business.Models;

// When the swap succeeded: the notes of the month in their new board order, and the new versions of the two notes.
public sealed record NoteOrderResult(NoteOrderStatus Status, IReadOnlyList<int>? NoteIds = null,
    IReadOnlyList<NoteVersionChange>? Versions = null);
