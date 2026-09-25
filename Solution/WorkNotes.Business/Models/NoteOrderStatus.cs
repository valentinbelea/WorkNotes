namespace WorkNotes.Business.Models;

// Stable result codes of a swap on the board; the presentation layer maps them to localized messages.
public enum NoteOrderStatus
{
    Saved,
    NotFound,
    Forbidden,
    // The target is the note itself or a note of another board or month.
    InvalidTarget,
    // One of the two notes changed after it was read; nothing was saved.
    Conflict
}
