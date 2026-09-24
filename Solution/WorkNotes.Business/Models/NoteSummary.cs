namespace WorkNotes.Business.Models;

// What a board card shows for one note; Preview is the start of its text, IsOwner describes the current user.
public sealed record NoteSummary(
    int Id,
    int ContextId,
    string NoteType,
    string? Title,
    string? Preview,
    DateOnly? JournalDate,
    string Visibility,
    DateTime CreatedAtUtc,
    bool IsOwner);
