using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WorkNotes.Business.Models;
using WorkNotes.DataAccess.Entities;

namespace WorkNotes.DataAccess.Context;

// Maps the existing Identity schema, maintained through Database First SQL scripts.
public sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options)
    : IdentityUserContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(u => u.Id).HasMaxLength(128);
            entity.Property(u => u.FirstName).HasMaxLength(AccountRules.NameMaxLength).IsRequired();
            entity.Property(u => u.LastName).HasMaxLength(AccountRules.NameMaxLength).IsRequired();
            entity.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex")
                .IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
        });
    }
}
