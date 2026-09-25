namespace WorkNotes.Business.Models;

// A validated save of the whole note: the paragraphs in document order replace the stored ones.
public sealed record NoteChanges(
    int NoteId,
    string OwnerUserId,
    string ExpectedVersion,
    string? Title,
    IReadOnlyList<NoteBlockInput> Blocks,
    DateTime SavedAtUtc);
