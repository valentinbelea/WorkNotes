namespace WorkNotes.Business.Models;

// A note opened in the editor. Version is an opaque concurrency token returned with every save.
// ModifiedAtUtc is the note's last change (the creation time until it is first changed).
public sealed record NoteDocument(
    int Id,
    int ContextId,
    string ContextName,
    string NoteType,
    string? Title,
    string Visibility,
    bool IsOwner,
    DateTime CreatedAtUtc,
    DateTime ModifiedAtUtc,
    string Version,
    IReadOnlyList<NoteBlockDetails> Blocks);
