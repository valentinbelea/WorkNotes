namespace WorkNotes.Business.Models;

// A note opened in the editor. Version is an opaque concurrency token returned with every save.
// ModifiedAtUtc is the note's last change (the creation time until it is first changed). References are the notes the
// links of its paragraphs open for the user (INoteReferenceService.WithLinksAsync fills them and the links).
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
    IReadOnlyList<NoteBlockDetails> Blocks,
    IReadOnlyList<NoteReferenceTarget>? References = null);
