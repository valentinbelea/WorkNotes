namespace WorkNotes.Business.Models;

// A note a reference can open for the current user: another note of the same board that the user may see.
public sealed record NoteReferenceTarget(int Id, string? Title, string NoteType);
