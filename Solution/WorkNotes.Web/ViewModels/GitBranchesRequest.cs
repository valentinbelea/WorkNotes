namespace WorkNotes.Web.ViewModels;

// JSON body sent by note-references.js when the owner asks for the branches of a repository that contain a reference:
// the repository's ID at GitHub and the reference as the lookup gave it (CR:30080).
public sealed class GitBranchesRequest
{
    public string? Repository { get; set; }
    public string? Reference { get; set; }
}
