namespace WorkNotes.Business.Models;

// When the save succeeded: the note's new concurrency token, the audit of its paragraphs in document order,
// and the note's last change.
public sealed record NoteSaveResult(NoteSaveStatus Status, string? Version = null, IReadOnlyList<NoteBlockAudit>? Blocks = null,
    DateTime? ModifiedAtUtc = null);
