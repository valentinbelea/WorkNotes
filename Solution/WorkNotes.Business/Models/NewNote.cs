namespace WorkNotes.Business.Models;

// A validated note ready to be stored; the owner is also its creator and last editor.
public sealed record NewNote(int ContextId, string OwnerUserId, string NoteType, string? Title, DateOnly? JournalDate, string Visibility);
