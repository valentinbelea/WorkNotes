namespace WorkNotes.Business.Models;

// The rules of the Git references of a note: which branch names belong to a reference, and the limits of the branch lists.
public static class GitReferenceRules
{
    // The column of dbo.GitReferences.Name.
    public const int BranchNameMaxLength = 255;
    // How many branches of a repository are read to look for a reference (10 pages of 100): a repository with more says so.
    public const int MaxBranchesRead = 1000;
    // How many matching branches the editor's popup lists.
    public const int MaxBranchesShown = 50;

    // A branch name the application stores and shows: not empty, no longer than the column, no control characters.
    public static bool ValidBranchName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= BranchNameMaxLength && !name.Any(char.IsControl);

    // The reference (CR:30080) as NoteReferenceRules.Normalize writes it, when its type is one the parser reads and its
    // number is written without leading zeros; null for anything else.
    public static NoteReferenceMatch? ParseReference(NoteReferenceParser parser, string? normalizedReference)
    {
        if (string.IsNullOrEmpty(normalizedReference) || normalizedReference.Count(character => character == ':') != 1) return null;
        var found = parser.Find(normalizedReference.Replace(':', ' '));
        return found is [{ Start: 0 } match] && match.NormalizedReference == normalizedReference
            && match.Length == normalizedReference.Length ? match : null;
    }

    // Whether the name of a branch contains the reference by the rules of the notes (NoteReferenceParser): the type and
    // the number as whole words, joined by "-", "_" or nothing (feature/CR-30080-export, CR_30080, bug1234).
    public static bool BranchMatches(NoteReferenceParser parser, string branchName, string normalizedReference) =>
        parser.Find(branchName).Any(match => match.NormalizedReference == normalizedReference);

    // Whether a text (a paragraph) writes the reference.
    public static bool Writes(NoteReferenceParser parser, string? text, string normalizedReference) =>
        parser.Find(text).Any(match => match.NormalizedReference == normalizedReference);

    // The page of a branch at the provider: the repository's address, /tree/, and the branch name with each part of its
    // path encoded (feature/CR_1 stays a path).
    public static string BranchUrl(string repositoryUrl, string branchName) =>
        repositoryUrl.TrimEnd('/') + "/tree/" + string.Join('/', branchName.Split('/').Select(Uri.EscapeDataString));
}
