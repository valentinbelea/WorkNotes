namespace WorkNotes.Business.Models;

// What a board card shows for one note; Preview is the start of its text, IsOwner describes the current user.
// ModifiedAtUtc is null while the note has not been changed since it was created.
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
    bool IsOwner)
{
    // The board groups and orders notes by their last change: ISNULL(ModifiedAtUtc, CreatedAtUtc).
    public DateTime LastChangedAtUtc => ModifiedAtUtc ?? CreatedAtUtc;
}
