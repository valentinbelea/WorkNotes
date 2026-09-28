namespace WorkNotes.Business.Models;

// What creating the references of the existing notes did to one note.
public enum NoteReferenceBackfillStatus
{
    // No number of the text has a single target.
    Unchanged,
    // The numbers with a single target became references (in a preview: would become).
    Linked,
    // The note changed after it was read; nothing was saved. Running again reads it anew.
    Conflict,
    // With the references the text would be longer than a save accepts (NoteRules.MaxContentLength); nothing was saved.
    TooLong
}
