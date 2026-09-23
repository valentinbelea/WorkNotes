using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// Reads and changes only contexts in which userId is a member; other contexts behave as missing.
public interface IWorkContextRepository
{
    Task<IReadOnlyList<WorkContext>> GetForMemberAsync(string userId, CancellationToken cancellationToken);
    Task<WorkContext?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken);
    // Saves the context and its owner membership atomically.
    Task<WorkContextSaveStatus> AddAsync(string name, string? description, string ownerUserId, CancellationToken cancellationToken);
    Task<WorkContextSaveStatus> UpdateAsync(int id, string userId, string name, string? description, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, string userId, CancellationToken cancellationToken);
}
