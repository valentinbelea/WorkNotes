namespace WorkNotes.Web.ViewModels;

// JSON body sent by note-references.js when a word ends right after a number: the text of the line up to there.
public sealed class NoteReferenceLookupRequest
{
    public string? Text { get; set; }
}
