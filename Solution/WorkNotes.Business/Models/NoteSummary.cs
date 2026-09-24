namespace WorkNotes.Business.Models;

// What a board card shows for one note; IsOwner describes the current user.
public sealed record NoteSummary(
    int Id,
    string NoteType,
    string? Title,
    DateOnly? JournalDate,
    string Visibility,
    DateTime CreatedAtUtc,
    bool IsOwner);
