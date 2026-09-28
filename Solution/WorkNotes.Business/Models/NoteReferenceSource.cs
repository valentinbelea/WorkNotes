namespace WorkNotes.Business.Models;

// A note that can get references: its board and its owner, the only one who writes in it.
public sealed record NoteReferenceSource(int NoteId, int ContextId, string OwnerUserId);
