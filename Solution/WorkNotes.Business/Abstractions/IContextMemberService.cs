using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// Only the Owner of a context may see its member list and add members.
public interface IContextMemberService
{
    // Null when the context is missing, outside the user's membership, or not owned by the user.
    Task<IReadOnlyList<ContextMemberDetails>?> GetMembersAsync(int contextId, string userId, CancellationToken cancellationToken);
    Task<ContextMemberAddStatus> AddMemberAsync(int contextId, string userId, string email, CancellationToken cancellationToken);
}
