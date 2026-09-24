namespace WorkNotes.Business.Models;

// When the save succeeded: the note's new concurrency token and the audit of its paragraphs, in document order.
public sealed record NoteSaveResult(NoteSaveStatus Status, string? Version = null, IReadOnlyList<NoteBlockAudit>? Blocks = null);
