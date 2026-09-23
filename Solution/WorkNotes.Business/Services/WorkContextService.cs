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

    public async Task<WorkContextSaveStatus> UpdateAsync(int id, string userId, string name, string? description, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (Validate(name, description) is { } invalid) return invalid;
        var context = await repository.GetByIdAsync(id, userId, cancellationToken);
        if (context is null) return WorkContextSaveStatus.NotFound;
        if (!context.IsOwner) return WorkContextSaveStatus.Forbidden;
        return await repository.UpdateAsync(id, userId, name.Trim(), WorkContextRules.NormalizeDescription(description), cancellationToken);
    }

    public async Task<WorkContextDeleteStatus> DeleteAsync(int id, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var context = await repository.GetByIdAsync(id, userId, cancellationToken);
        if (context is null) return WorkContextDeleteStatus.NotFound;
        if (!context.IsOwner) return WorkContextDeleteStatus.Forbidden;
        return await repository.DeleteAsync(id, userId, cancellationToken)
            ? WorkContextDeleteStatus.Deleted
            : WorkContextDeleteStatus.NotFound;
    }

    private static WorkContextSaveStatus? Validate(string? name, string? description) =>
        !WorkContextRules.ValidName(name) ? WorkContextSaveStatus.InvalidName
        : !WorkContextRules.ValidDescription(description) ? WorkContextSaveStatus.InvalidDescription
        : null;
}
