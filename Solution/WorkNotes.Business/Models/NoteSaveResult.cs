namespace WorkNotes.Business.Models;

// Version is the note's new concurrency token when the save succeeded.
public sealed record NoteSaveResult(NoteSaveStatus Status, string? Version = null);
