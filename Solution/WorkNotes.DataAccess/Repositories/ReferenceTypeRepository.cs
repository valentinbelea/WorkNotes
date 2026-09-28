using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.DataAccess.Context;

namespace WorkNotes.DataAccess.Repositories;

public sealed class ReferenceTypeRepository(WorkNotesDbContext dbContext) : IReferenceTypeRepository
{
    public async Task<IReadOnlyList<string>> GetActiveTypesAsync(CancellationToken cancellationToken) =>
        await dbContext.ReferenceTypes
            .AsNoTracking()
            .Where(type => type.IsActive)
            .OrderBy(type => type.Code)
            .Select(type => type.Code)
            .ToListAsync(cancellationToken);
}
