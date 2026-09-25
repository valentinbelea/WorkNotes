namespace WorkNotes.Business.Models;

// One month of a board, by the notes' local creation date; journals come before articles.
public sealed record NoteMonthGroup(int Year, int Month, IReadOnlyList<NoteSummary> Notes);
