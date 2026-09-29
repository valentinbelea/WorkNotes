namespace WorkNotes.Business.Models;

// A branch linked to a reference of a paragraph, as the editor shows it: NormalizedReference (CR:30080) is the
// reference of the paragraph, Url the page of the branch at the provider.
public sealed record NoteGitReference(int Id, Guid NoteBlockId, string NormalizedReference, string RepositoryFullName, string Name, string Url);
