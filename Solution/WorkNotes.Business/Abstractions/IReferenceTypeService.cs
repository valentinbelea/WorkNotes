using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// The reference types configured in dbo.ReferenceTypes (CR, BUG...): the prefixes every text of the application is read
// with. They are kept in memory for the whole application and read again once ReferenceTypeCache.Duration has passed,
// so a change in the table shows within that time; within one request they stay the same.
public interface IReferenceTypeService
{
    // The parser of the active types.
    Task<NoteReferenceParser> GetParserAsync(CancellationToken cancellationToken);
}
