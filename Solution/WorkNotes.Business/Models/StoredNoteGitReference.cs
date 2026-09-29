namespace WorkNotes.Business.Models;

// A link as the repository reads it, with the paragraph it belongs to (its place in the note and its text), so the
// service shows the links whose reference the paragraph still writes.
public sealed record StoredNoteGitReference(int Id, Guid NoteBlockId, int BlockPosition, string BlockContent, string NormalizedReference,
    string RepositoryFullName, string RepositoryUrl, string Name);
