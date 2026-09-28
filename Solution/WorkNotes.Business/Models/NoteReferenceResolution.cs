namespace WorkNotes.Business.Models;

// The references a save stores for the paragraphs of a note, and the notes they open.
public sealed record NoteReferenceResolution(IReadOnlyList<NoteBlockReference> References, IReadOnlyList<NoteReferenceTarget> Targets);
