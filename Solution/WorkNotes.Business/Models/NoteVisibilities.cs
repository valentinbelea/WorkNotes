namespace WorkNotes.Business.Models;

// Private: only the owner sees the note. Context: members of the note's context see it; only the owner edits it.
public static class NoteVisibilities
{
    public const string Private = "Private";
    public const string Context = "Context";
}
