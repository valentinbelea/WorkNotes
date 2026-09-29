namespace WorkNotes.Web.ViewModels;

// JSON body sent by note-editor.js when the owner removes a link of a branch: its id.
public sealed class GitReferenceRemoveRequest
{
    public int Id { get; set; }
}
