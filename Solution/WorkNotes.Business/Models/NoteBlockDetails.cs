namespace WorkNotes.Business.Models;

// One paragraph of a note with its audit dates (UTC).
public sealed record NoteBlockDetails(Guid Id, string Content, DateTime CreatedAtUtc, DateTime ModifiedAtUtc);
