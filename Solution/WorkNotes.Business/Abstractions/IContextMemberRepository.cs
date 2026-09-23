using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

public interface IContextMemberRepository
{
    Task<IReadOnlyList<ContextMemberDetails>> GetMembersAsync(int contextId, CancellationToken cancellationToken);
    // Finds the account by email as Identity normalizes it; returns UserNotFound, AlreadyMember or Added.
    Task<ContextMemberAddStatus> AddByEmailAsync(int contextId, string email, string role, CancellationToken cancellationToken);
}
