using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface IWorkContextRepository
{
    Task<IReadOnlyList<WorkContext>> GetAllAsync(CancellationToken cancellationToken);
    Task<WorkContext?> GetByIdAsync(int id, CancellationToken cancellationToken);
    // Saves the context and its owner membership atomically.
    Task<WorkContextSaveStatus> AddAsync(string name, string? description, string ownerUserId, CancellationToken cancellationToken);
    Task<WorkContextSaveStatus> UpdateAsync(int id, string name, string? description, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
