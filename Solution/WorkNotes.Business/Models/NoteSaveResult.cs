namespace WorkNotes.Business.Models;

// When the save succeeded: the note's new concurrency token, the audit and links of its paragraphs in document order,
// the note's last change, and the notes the links open.
public sealed record NoteSaveResult(NoteSaveStatus Status, string? Version = null, IReadOnlyList<NoteBlockAudit>? Blocks = null,
    DateTime? ModifiedAtUtc = null, IReadOnlyList<NoteReferenceTarget>? References = null);
