namespace WorkNotes.Business.Models;

// Title is the stored (normalized) title when the rename succeeded; null means "untitled".
public sealed record NoteRenameResult(NoteSaveStatus Status, string? Title = null);
