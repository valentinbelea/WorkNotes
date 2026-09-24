namespace WorkNotes.Business.Models;

// A paragraph as the editor sends it: the id is kept for existing paragraphs and new for new ones.
public sealed record NoteBlockInput(Guid Id, string Content);
