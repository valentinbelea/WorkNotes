using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Context;
using NoteEntity = WorkNotes.DataAccess.Entities.Note;

namespace WorkNotes.DataAccess.Repositories;

public sealed class NoteRepository(WorkNotesDbContext dbContext) : INoteRepository
{
    public async Task<IReadOnlyList<NoteSummary>> GetBoardAsync(string userId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Notes
            .AsNoTracking()
            // Every note requires membership of its context; shared notes are visible to all its members.
            .Where(note => note.ArchivedAtUtc == null
                && note.Context.ContextMembers.Any(member => member.UserId == userId)
                && (note.OwnerUserId == userId || note.Visibility == NoteVisibilities.Context))
            .OrderByDescending(note => note.CreatedAtUtc)
            .ThenByDescending(note => note.Id)
            .Select(note => new NoteSummary(note.Id, note.Context.Name, note.NoteType, note.Title, note.JournalDate,
                note.Visibility, note.CreatedAtUtc, note.OwnerUserId == userId))
            .ToListAsync(cancellationToken);
        // SQL Server returns datetime2 without a kind; the column stores UTC.
        return rows.Select(note => note with { CreatedAtUtc = DateTime.SpecifyKind(note.CreatedAtUtc, DateTimeKind.Utc) }).ToList();
    }

    public async Task<NoteCreateStatus> AddAsync(NewNote note, CancellationToken cancellationToken)
    {
        var entity = new NoteEntity
        {
            ContextId = note.ContextId,
            OwnerUserId = note.OwnerUserId,
            NoteType = note.NoteType,
            Title = note.Title,
            JournalDate = note.JournalDate,
            Visibility = note.Visibility,
            CreatedByUserId = note.OwnerUserId,
            ModifiedByUserId = note.OwnerUserId
        };
        dbContext.Notes.Add(entity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return NoteCreateStatus.Created;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // UX_Notes_DailyJournal: one journal per owner, context and day, also under concurrent saves.
            dbContext.Entry(entity).State = EntityState.Detached;
            return NoteCreateStatus.JournalExists;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            // The context was deleted after the membership check.
            dbContext.Entry(entity).State = EntityState.Detached;
            return NoteCreateStatus.ContextNotFound;
        }
    }
}
