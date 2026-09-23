namespace WorkNotes.Business.Models;

// What the board shows for one note; IsOwner describes the current user.
public sealed record NoteSummary(
    int Id,
    string ContextName,
    string NoteType,
    string? Title,
    DateOnly? JournalDate,
    string Visibility,
    DateTime CreatedAtUtc,
    bool IsOwner);
