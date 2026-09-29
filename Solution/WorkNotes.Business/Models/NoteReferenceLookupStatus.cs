namespace WorkNotes.Business.Models;

// What the editor's lookup of a reference just typed found at the end of the text.
public enum NoteReferenceLookupStatus
{
    // A reference that opens at least one note.
    Found,
    // A reference no other note of the board has in its title: it stays plain text.
    NoNote,
    // No reference ends the text.
    NoReference,
    NotFound,
    Forbidden
}
