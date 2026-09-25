namespace WorkNotes.Business.Models;

// Note is the renamed note as the board shows it: the stored (normalized) title, null meaning "untitled",
// and the new modification time.
public sealed record NoteRenameResult(NoteSaveStatus Status, NoteSummary? Note = null);
