using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// A user sees and changes only the contexts in which they are a member.
public interface IWorkContextService
{
    Task<IReadOnlyList<WorkContext>> GetForMemberAsync(string userId, CancellationToken cancellationToken);
    Task<WorkContext?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken);
    // The creator becomes the context's first member, with the Owner role.
    Task<WorkContextSaveStatus> CreateAsync(string name, string? description, string creatorUserId, CancellationToken cancellationToken);
    Task<WorkContextSaveStatus> UpdateAsync(int id, string userId, string name, string? description, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, string userId, CancellationToken cancellationToken);
}
