namespace WorkNotes.Business.Models;

// When the save succeeded: the note's new concurrency token, the audit and links of its paragraphs in document order,
// the note's last change, and the notes the links open. Note is the saved note as its card on the board shows it now;
// Month is set only when the save moved the note to another month of its board (the current one): that month as the
// board now shows it, the note included.
public sealed record NoteSaveResult(NoteSaveStatus Status, string? Version = null, IReadOnlyList<NoteBlockAudit>? Blocks = null,
    DateTime? ModifiedAtUtc = null, IReadOnlyList<NoteReferenceTarget>? References = null, NoteSummary? Note = null,
    NoteMonthGroup? Month = null);
