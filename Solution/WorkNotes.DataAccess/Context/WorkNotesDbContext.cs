using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using WorkNotes.DataAccess.Entities;

namespace WorkNotes.DataAccess.Context;

public partial class WorkNotesDbContext : DbContext
{
    public WorkNotesDbContext(DbContextOptions<WorkNotesDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ContextMember> ContextMembers { get; set; }

    public virtual DbSet<DatabaseVersion> DatabaseVersions { get; set; }

    public virtual DbSet<WorkContext> WorkContexts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContextMember>(entity =>
        {
            entity.HasKey(e => new { e.ContextId, e.UserId });

            entity.HasIndex(e => e.UserId, "IX_ContextMembers_UserId");

            entity.Property(e => e.UserId).HasMaxLength(128);
            entity.Property(e => e.AddedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ContextMembers_AddedAtUtc");
            entity.Property(e => e.Role).HasMaxLength(20);

            entity.HasOne(d => d.Context).WithMany(p => p.ContextMembers).HasForeignKey(d => d.ContextId);
        });

        modelBuilder.Entity<DatabaseVersion>(entity =>
        {
            entity.HasKey(e => e.Version);

            entity.ToTable("DatabaseVersion");

            entity.Property(e => e.Version).HasMaxLength(50);
        });

        modelBuilder.Entity<WorkContext>(entity =>
        {
            entity.HasIndex(e => e.Name, "UX_WorkContexts_Name").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
