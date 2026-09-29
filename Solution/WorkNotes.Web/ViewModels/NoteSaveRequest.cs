namespace WorkNotes.Web.ViewModels;

// JSON body sent by note-editor.js: the whole note, paragraphs in document order. NoteType is the type chosen in the
// editor's footer (null keeps the stored one).
public sealed class NoteSaveRequest
{
    public string Version { get; set; } = "";
    public string? Title { get; set; }
    public string? NoteType { get; set; }
    public List<NoteBlockRequest>? Blocks { get; set; } = [];
}

public sealed class NoteBlockRequest
{
    public Guid Id { get; set; }
    public string Content { get; set; } = "";
}
