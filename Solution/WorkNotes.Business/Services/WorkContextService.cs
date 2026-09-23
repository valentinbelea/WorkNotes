using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class WorkContextService(IWorkContextRepository repository) : IWorkContextService
{
    public Task<IReadOnlyList<WorkContext>> GetForMemberAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        return repository.GetForMemberAsync(userId, cancellationToken);
    }

    public Task<WorkContext?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        return repository.GetByIdAsync(id, userId, cancellationToken);
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

    public Task<WorkContextSaveStatus> UpdateAsync(int id, string userId, string name, string? description, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var invalid = Validate(name, description);
        return invalid is { } status
            ? Task.FromResult(status)
            : repository.UpdateAsync(id, userId, name.Trim(), WorkContextRules.NormalizeDescription(description), cancellationToken);
    }

    public Task<bool> DeleteAsync(int id, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        return repository.DeleteAsync(id, userId, cancellationToken);
    }

    private static WorkContextSaveStatus? Validate(string? name, string? description) =>
        !WorkContextRules.ValidName(name) ? WorkContextSaveStatus.InvalidName
        : !WorkContextRules.ValidDescription(description) ? WorkContextSaveStatus.InvalidDescription
        : null;
}
