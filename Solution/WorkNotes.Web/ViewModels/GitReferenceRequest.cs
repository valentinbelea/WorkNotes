namespace WorkNotes.Web.ViewModels;

// JSON body sent by note-editor.js when the owner links a branch to a reference of a paragraph: the paragraph, the
// repository's ID at GitHub, the reference (CR:30080) and the branch.
public sealed class GitReferenceRequest
{
    public Guid BlockId { get; set; }
    public string? Repository { get; set; }
    public string? Reference { get; set; }
    public string? Branch { get; set; }
}
