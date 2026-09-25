namespace WorkNotes.Business.Models;

// A validated save of the whole note: the paragraphs in document order replace the stored ones, and References (those
// in the text whose target the owner may open) replace the note's stored references.
public sealed record NoteChanges(
    int NoteId,
    string OwnerUserId,
    string ExpectedVersion,
    string? Title,
    IReadOnlyList<NoteBlockInput> Blocks,
    IReadOnlyList<NoteReferenceInput> References,
    DateTime SavedAtUtc);
