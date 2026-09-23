using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface IWorkContextService
{
    Task<IReadOnlyList<WorkContext>> GetAllAsync(CancellationToken cancellationToken);
    Task<WorkContext?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<WorkContextSaveStatus> CreateAsync(string name, string? description, CancellationToken cancellationToken);
    Task<WorkContextSaveStatus> UpdateAsync(int id, string name, string? description, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
