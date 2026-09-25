using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// Only the Owner of a context may see its member list, add members and remove them.
public interface IContextMemberService
{
    // Null when the context is missing, outside the user's membership, or not owned by the user.
    Task<IReadOnlyList<ContextMemberDetails>?> GetMembersAsync(int contextId, string userId, CancellationToken cancellationToken);
    Task<ContextMemberAddStatus> AddMemberAsync(int contextId, string userId, string email, CancellationToken cancellationToken);
    // The Owner cannot be removed; removed members lose access to the context immediately.
    Task<ContextMemberRemoveStatus> RemoveMemberAsync(int contextId, string userId, string memberUserId, CancellationToken cancellationToken);
}
