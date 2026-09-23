using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface INoteService
{
    Task<IReadOnlyList<NoteSummary>> GetBoardAsync(string userId, CancellationToken cancellationToken);
    // Creates a Private note in a context the user belongs to; a Journal is dated today.
    Task<NoteCreateStatus> CreateAsync(string userId, int contextId, string noteType, string? title, CancellationToken cancellationToken);
}
