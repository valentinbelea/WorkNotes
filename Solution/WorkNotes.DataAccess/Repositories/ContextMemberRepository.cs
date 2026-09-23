using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Context;
using ContextMemberEntity = WorkNotes.DataAccess.Entities.ContextMember;

namespace WorkNotes.DataAccess.Repositories;

// Memberships live in WorkNotesDbContext and accounts in AccountsDbContext, so the two are read separately.
public sealed class ContextMemberRepository(WorkNotesDbContext dbContext, AccountsDbContext accounts, ILookupNormalizer normalizer)
    : IContextMemberRepository
{
    public async Task<IReadOnlyList<ContextMemberDetails>> GetMembersAsync(int contextId, CancellationToken cancellationToken)
    {
        var memberships = await dbContext.ContextMembers
            .AsNoTracking()
            .Where(member => member.ContextId == contextId)
            .Select(member => new { member.UserId, member.Role, member.AddedAtUtc })
            .ToListAsync(cancellationToken);
        var userIds = memberships.Select(member => member.UserId).ToList();
        var users = await accounts.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FirstName, user.LastName, user.Email })
            .ToDictionaryAsync(user => user.Id, cancellationToken);
        return memberships
            .Where(member => users.ContainsKey(member.UserId))
            .OrderBy(member => member.Role == ContextRoles.Owner ? 0 : 1)
            .ThenBy(member => member.AddedAtUtc)
            .Select(member =>
            {
                var user = users[member.UserId];
                return new ContextMemberDetails(user.Id, user.FirstName, user.LastName, user.Email ?? "", member.Role);
            })
            .ToList();
    }

    public async Task<ContextMemberAddStatus> AddByEmailAsync(int contextId, string email, string role, CancellationToken cancellationToken)
    {
        var normalizedEmail = normalizer.NormalizeEmail(email);
        var userId = await accounts.Users
            .AsNoTracking()
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .Select(user => user.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is null) return ContextMemberAddStatus.UserNotFound;
        if (await dbContext.ContextMembers.AnyAsync(member => member.ContextId == contextId && member.UserId == userId, cancellationToken))
            return ContextMemberAddStatus.AlreadyMember;

        var entity = new ContextMemberEntity { ContextId = contextId, UserId = userId, Role = role };
        dbContext.ContextMembers.Add(entity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ContextMemberAddStatus.Added;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 547 } sql)
        {
            // 2627: added concurrently by another request; 547: the account or context was deleted meanwhile.
            dbContext.Entry(entity).State = EntityState.Detached;
            return sql.Number == 2627 ? ContextMemberAddStatus.AlreadyMember : ContextMemberAddStatus.UserNotFound;
        }
    }

    public async Task<bool> RemoveMemberAsync(int contextId, string memberUserId, CancellationToken cancellationToken) =>
        // The Role filter keeps Owner memberships out of reach even if called with an Owner's id.
        await dbContext.ContextMembers
            .Where(member => member.ContextId == contextId && member.UserId == memberUserId && member.Role == ContextRoles.Member)
            .ExecuteDeleteAsync(cancellationToken) > 0;
}
