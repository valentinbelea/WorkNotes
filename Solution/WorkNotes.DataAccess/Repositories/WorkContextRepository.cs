using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Context;
using ContextMemberEntity = WorkNotes.DataAccess.Entities.ContextMember;
using WorkContextEntity = WorkNotes.DataAccess.Entities.WorkContext;

namespace WorkNotes.DataAccess.Repositories;

public sealed class WorkContextRepository(WorkNotesDbContext dbContext) : IWorkContextRepository
{
    public async Task<IReadOnlyList<WorkContext>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.WorkContexts
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new WorkContext(item.Id, item.Name, item.Description))
            .ToListAsync(cancellationToken);

    public async Task<WorkContext?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await dbContext.WorkContexts
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new WorkContext(item.Id, item.Name, item.Description))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<WorkContextSaveStatus> AddAsync(string name, string? description, string ownerUserId, CancellationToken cancellationToken)
    {
        var entity = new WorkContextEntity { Name = name, Description = description };
        // One SaveChanges inserts the context and the owner membership in a single transaction.
        entity.ContextMembers.Add(new ContextMemberEntity { UserId = ownerUserId, Role = ContextRoles.Owner });
        dbContext.WorkContexts.Add(entity);
        return await SaveAsync(entity, cancellationToken);
    }

    public async Task<WorkContextSaveStatus> UpdateAsync(int id, string name, string? description, CancellationToken cancellationToken)
    {
        var entity = await dbContext.WorkContexts.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return WorkContextSaveStatus.NotFound;
        entity.Name = name;
        entity.Description = description;
        return await SaveAsync(entity, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        await dbContext.WorkContexts.Where(item => item.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    private async Task<WorkContextSaveStatus> SaveAsync(WorkContextEntity entity, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return WorkContextSaveStatus.Saved;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The unique index on Name also protects concurrent saves.
            dbContext.Entry(entity).State = EntityState.Detached;
            return WorkContextSaveStatus.DuplicateName;
        }
    }
}
