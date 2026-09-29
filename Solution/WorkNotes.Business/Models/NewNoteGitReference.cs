namespace WorkNotes.Business.Models;

// A branch to link to a reference of a paragraph. The repository adds the branch to the catalog of Git references when
// it is not there (updating the repository's name and address) and the reference to the catalog of references, then the
// link, unless the paragraph has it already.
public sealed record NewNoteGitReference(Guid NoteBlockId, string ReferenceType, long ReferenceNumber, string Provider,
    string RepositoryId, string RepositoryFullName, string RepositoryUrl, string BranchName, string CreatedByUserId, DateTime CreatedAtUtc)
{
    public string NormalizedReference => NoteReferenceRules.Normalize(ReferenceType, ReferenceNumber);
}
