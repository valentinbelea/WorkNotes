using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class WorkContextService(IWorkContextRepository repository) : IWorkContextService
{
    public Task<IReadOnlyList<WorkContext>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return repository.GetAllAsync(cancellationToken);
    }

    public Task<WorkContext?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return repository.GetByIdAsync(id, cancellationToken);
    }

    public Task<WorkContextSaveStatus> CreateAsync(string name, string? description, string creatorUserId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creatorUserId);
        cancellationToken.ThrowIfCancellationRequested();
        var invalid = Validate(name, description);
        return invalid is { } status
            ? Task.FromResult(status)
            : repository.AddAsync(name.Trim(), WorkContextRules.NormalizeDescription(description), creatorUserId, cancellationToken);
    }

    public Task<WorkContextSaveStatus> UpdateAsync(int id, string name, string? description, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var invalid = Validate(name, description);
        return invalid is { } status
            ? Task.FromResult(status)
            : repository.UpdateAsync(id, name.Trim(), WorkContextRules.NormalizeDescription(description), cancellationToken);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return repository.DeleteAsync(id, cancellationToken);
    }

    private static WorkContextSaveStatus? Validate(string? name, string? description) =>
        !WorkContextRules.ValidName(name) ? WorkContextSaveStatus.InvalidName
        : !WorkContextRules.ValidDescription(description) ? WorkContextSaveStatus.InvalidDescription
        : null;
}
