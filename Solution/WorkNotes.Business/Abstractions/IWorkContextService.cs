using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface IWorkContextService
{
    Task<IReadOnlyList<WorkContext>> GetAllAsync(CancellationToken cancellationToken);
    Task<WorkContext?> GetByIdAsync(int id, CancellationToken cancellationToken);
    // The creator becomes the context's first member, with the Owner role.
    Task<WorkContextSaveStatus> CreateAsync(string name, string? description, string creatorUserId, CancellationToken cancellationToken);
    Task<WorkContextSaveStatus> UpdateAsync(int id, string name, string? description, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
