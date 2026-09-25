namespace WorkNotes.Business.Models;

// Creation and last change of one paragraph (UTC), as stored after a save.
public sealed record NoteBlockAudit(Guid Id, DateTime CreatedAtUtc, DateTime ModifiedAtUtc);
