namespace WorkNotes.Business.Models;

// A paragraph whose stored references a change of its board may change (INoteReferenceService.RefreshAsync): its note,
// the note's owner (references follow what the owner may see), its text and its version as it was read.
public sealed record NoteReferenceSource(Guid NoteBlockId, int NoteId, string OwnerUserId, string Content, string Version);
