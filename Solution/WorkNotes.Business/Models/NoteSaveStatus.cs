namespace WorkNotes.Business.Models;

// Stable result codes; the presentation layer maps them to localized messages.
public enum NoteSaveStatus
{
    Saved,
    NotFound,
    Forbidden,
    Conflict,
    InvalidTitle,
    InvalidContent
}
