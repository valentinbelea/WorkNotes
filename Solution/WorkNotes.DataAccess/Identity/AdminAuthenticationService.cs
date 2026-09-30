using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Context;
using WorkNotes.DataAccess.Entities;

namespace WorkNotes.DataAccess.Identity;

public sealed class AdminAuthenticationService(WorkNotesDbContext dbContext, IPasswordHasher<AdminUser> passwordHasher)
    : IAdminAuthenticationService, IAdminBootstrapService
{
    public async Task<AdminAuthenticationResult?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var normalized = userName.Trim();
        var admin = await dbContext.AdminUsers.FirstOrDefaultAsync(x => x.UserName == normalized && x.IsActive, cancellationToken);
        if (admin is null || passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password) == PasswordVerificationResult.Failed)
            return null;

        admin.LastLoginAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(admin.Id, admin.UserName);
    }

    public async Task CreateIfMissingAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var normalized = userName.Trim();
        if (await dbContext.AdminUsers.AsNoTracking().AnyAsync(x => x.UserName == normalized, cancellationToken)) return;
        var admin = new AdminUser { UserName = normalized, IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        dbContext.AdminUsers.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
