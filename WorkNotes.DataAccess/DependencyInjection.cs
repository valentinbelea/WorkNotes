using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkNotes.Business.Abstractions;
using WorkNotes.DataAccess.Context;
using WorkNotes.DataAccess.Repositories;

namespace WorkNotes.DataAccess;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<WorkNotesDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationVersionRepository, ApplicationVersionRepository>();

        return services;
    }
}
