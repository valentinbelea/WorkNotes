namespace WorkNotes.Business.Models;

// One month of a board, by the local date of the notes' last change; the notes are in their board order.
public sealed record NoteMonthGroup(int Year, int Month, IReadOnlyList<NoteSummary> Notes);
