namespace WorkNotes.Business.Models;

// The result of the editor's quick action for a missing BUG/CR reference. Existing means another request created the
// article before this one reached the server; in both successful cases Targets contains the article to open.
public sealed record NoteReferenceArticleResult(NoteReferenceArticleStatus Status,
    IReadOnlyList<NoteReferenceTarget>? Targets = null);

public enum NoteReferenceArticleStatus
{
    Created,
    Existing,
    InvalidReference,
    Forbidden,
    NotFound,
    InvalidTitle,
    ContextNotFound
}
