namespace WorkNotes.Business.Models;

// What a board card shows for one note; Preview is the start of its text, IsOwner describes the current user.
// ModifiedAtUtc is null while the note has not been changed since it was created. Order places the note in its month
// on the board (smaller first); Version is the note's concurrency token when it was read. References are the notes the
// references in the preview can open for the user (the board fills them; a reference to any other note is shown as one
// that can no longer be opened).
public sealed record NoteSummary(
    int Id,
    int ContextId,
    string NoteType,
    string? Title,
    string? Preview,
    DateOnly? JournalDate,
    string Visibility,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    bool IsOwner,
    int Order,
    string Version,
    IReadOnlyList<NoteReferenceTarget>? References = null)
{
    // The board groups and orders notes by their last change: ISNULL(ModifiedAtUtc, CreatedAtUtc).
    public DateTime LastChangedAtUtc => ModifiedAtUtc ?? CreatedAtUtc;
}
