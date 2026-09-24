namespace WorkNotes.Business.Models;

// A note opened in the editor. Version is an opaque concurrency token returned with every save.
public sealed record NoteDocument(
    int Id,
    int ContextId,
    string ContextName,
    string NoteType,
    string? Title,
    string Visibility,
    bool IsOwner,
    DateTime CreatedAtUtc,
    string Version,
    IReadOnlyList<NoteBlockDetails> Blocks);
