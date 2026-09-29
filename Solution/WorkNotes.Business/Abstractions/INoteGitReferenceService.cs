using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// The Git references of a note: branches of the repositories the user imported (IGitRepositoryService) linked to the
// references (CR 30080, bug 1234) written in the note's paragraphs. In the editor's popup of a reference just typed the
// owner picks a repository, sees the branches whose name contains the reference by the rules of the notes
// (GitReferenceRules.BranchMatches) and links one to the paragraph. Only the owner of a note links or removes branches;
// whoever sees the note sees them. The GitHub calls are made in the owner's name with their token.
public interface INoteGitReferenceService
{
    // The branches of one of the user's imported repositories whose name contains the reference (normalized: CR:30080),
    // by name, at most GitReferenceRules.MaxBranchesShown, read from GitHub now.
    Task<GitBranchSearch> SearchBranchesAsync(string userId, int noteId, string repositoryId, string normalizedReference,
        CancellationToken cancellationToken);

    // Links a branch of one of the user's imported repositories to a reference of a paragraph of the note. GitHub must
    // have the branch, its name must contain the reference and the paragraph must write the reference; a link that exists
    // already succeeds without a change. The result has the note's links now.
    Task<NoteGitReferenceResult> AddAsync(string userId, int noteId, Guid blockId, string repositoryId, string normalizedReference,
        string branchName, CancellationToken cancellationToken);

    // Removes a link (only the owner); the result has the note's links now.
    Task<NoteGitReferenceResult> RemoveAsync(string userId, int noteId, int linkId, CancellationToken cancellationToken);

    // The links of a note the user may see whose reference their paragraph still writes, in document order; empty when the
    // user may not see the note.
    Task<IReadOnlyList<NoteGitReference>> GetAsync(string userId, int noteId, CancellationToken cancellationToken);
}
