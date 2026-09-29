using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Context;
using NoteEntity = WorkNotes.DataAccess.Entities.Note;

namespace WorkNotes.DataAccess.Repositories;

// The rule that decides which notes a user may see, shared by the repositories that read something of a note.
internal static class NoteAccess
{
    // Every note requires membership of its context; shared notes are visible to all its members.
    public static IQueryable<NoteEntity> VisibleNotes(this WorkNotesDbContext dbContext, string userId) =>
        dbContext.Notes.Where(note => note.ArchivedAtUtc == null
            && note.Context.ContextMembers.Any(member => member.UserId == userId)
            && (note.OwnerUserId == userId || note.Visibility == NoteVisibilities.Context));
}
