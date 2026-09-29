namespace WorkNotes.Business.Models;

// Creation and last change of one paragraph (UTC), as stored after a save, and where its saved text shows links.
public sealed record NoteBlockAudit(Guid Id, DateTime CreatedAtUtc, DateTime ModifiedAtUtc, IReadOnlyList<NoteReferenceLink>? Links = null);
