namespace WorkNotes.Business.Models;

// A validated save of the whole note: its title and type (a journal with its date, an article without one), the
// paragraphs in document order, which replace the stored ones, and References (one per paragraph and reference that
// opens at least one note, INoteReferenceService.ResolveAsync), which replace the stored references of its paragraphs.
public sealed record NoteChanges(
    int NoteId,
    string OwnerUserId,
    string ExpectedVersion,
    string? Title,
    string NoteType,
    DateOnly? JournalDate,
    IReadOnlyList<NoteBlockInput> Blocks,
    IReadOnlyList<NoteBlockReference> References,
    DateTime SavedAtUtc);
