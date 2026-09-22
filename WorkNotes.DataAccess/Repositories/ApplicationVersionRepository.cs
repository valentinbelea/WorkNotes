using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.DataAccess.Context;

namespace WorkNotes.DataAccess.Repositories;

public sealed class ApplicationVersionRepository(WorkNotesDbContext dbContext)
    : IApplicationVersionRepository
{
    public async Task<IReadOnlyList<string>> GetVersionsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.DatabaseVersions
            .AsNoTracking()
            .Select(item => item.Version)
            .ToListAsync(cancellationToken);
    }
}
