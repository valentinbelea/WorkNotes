namespace WorkNotes.Business.Models;

// One paragraph of a note with its audit dates (UTC) and, once they are read, where its text shows links.
public sealed record NoteBlockDetails(Guid Id, string Content, DateTime CreatedAtUtc, DateTime ModifiedAtUtc,
    IReadOnlyList<NoteReferenceLink>? Links = null);
