namespace WorkNotes.Business.Models;

// The references a save stores for the paragraphs of a note, the notes they open, and where each paragraph shows their
// links (by paragraph id; a paragraph without links is left out).
public sealed record NoteReferenceResolution(IReadOnlyList<NoteBlockReference> References, IReadOnlyList<NoteReferenceTarget> Targets,
    IReadOnlyDictionary<Guid, IReadOnlyList<NoteReferenceLink>> Links);
