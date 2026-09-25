using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkNotes.Business.Abstractions;
using WorkNotes.DataAccess.Context;
using WorkNotes.DataAccess.Repositories;
using Microsoft.AspNetCore.Identity;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Entities;
using WorkNotes.DataAccess.Identity;

namespace WorkNotes.DataAccess;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<WorkNotesDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationVersionRepository, ApplicationVersionRepository>();
        services.AddScoped<IWorkContextRepository, WorkContextRepository>();
        services.AddScoped<IContextMemberRepository, ContextMemberRepository>();
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddDbContext<AccountsDbContext>(options => options.UseSqlServer(connectionString));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Stores.MaxLengthForKeys = 128;
            options.User.AllowedUserNameCharacters = string.Empty;
            options.Password.RequiredLength = AccountRules.PasswordMinLength;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredUniqueChars = 1;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedAccount = false;
        })
            .AddEntityFrameworkStores<AccountsDbContext>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<AccountClaimsPrincipalFactory>();
        services.AddScoped<IdentityAccountService>();
        services.AddScoped<IAccountService>(provider => provider.GetRequiredService<IdentityAccountService>());
        services.AddScoped<IAuthenticationService>(provider => provider.GetRequiredService<IdentityAccountService>());

        return services;
    }
}
