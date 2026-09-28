namespace WorkNotes.Business.Models;

// A validated save of the whole note: the paragraphs in document order replace the stored ones, and References (one per
// paragraph and reference with a target, INoteReferenceService.ResolveAsync) replace the stored references of its
// paragraphs.
public sealed record NoteChanges(
    int NoteId,
    string OwnerUserId,
    string ExpectedVersion,
    string? Title,
    IReadOnlyList<NoteBlockInput> Blocks,
    IReadOnlyList<NoteBlockReference> References,
    DateTime SavedAtUtc);
