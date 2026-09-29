namespace WorkNotes.Business.Models;

// After adding or removing a link: the note's links now (as GetAsync gives them), when it succeeded.
public sealed record NoteGitReferenceResult(GitReferenceStatus Status, IReadOnlyList<NoteGitReference>? References = null);
