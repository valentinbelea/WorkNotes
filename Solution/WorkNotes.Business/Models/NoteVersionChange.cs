namespace WorkNotes.Business.Models;

// A note whose row changed but not its content (its place on the board): an editor that holds PreviousVersion is
// still up to date and goes on with Version.
public sealed record NoteVersionChange(int NoteId, string PreviousVersion, string Version);
