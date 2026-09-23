using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class ContextMemberService(IWorkContextRepository contexts, IContextMemberRepository members)
    : IContextMemberService
{
    public async Task<IReadOnlyList<ContextMemberDetails>?> GetMembersAsync(int contextId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        var context = await contexts.GetByIdAsync(contextId, userId, cancellationToken);
        if (context is not { IsOwner: true }) return null;
        return await members.GetMembersAsync(contextId, cancellationToken);
    }

    public async Task<ContextMemberAddStatus> AddMemberAsync(int contextId, string userId, string email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!AccountRules.ValidEmail(email)) return ContextMemberAddStatus.InvalidEmail;
        var context = await contexts.GetByIdAsync(contextId, userId, cancellationToken);
        if (context is null) return ContextMemberAddStatus.NotFound;
        if (!context.IsOwner) return ContextMemberAddStatus.Forbidden;
        // Members added by the Owner get the Member role; ownership is not transferred here.
        return await members.AddByEmailAsync(contextId, email.Trim(), ContextRoles.Member, cancellationToken);
    }
}
