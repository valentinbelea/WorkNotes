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

    public virtual DbSet<Note> Notes { get; set; }

    public virtual DbSet<NoteBlock> NoteBlocks { get; set; }

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

        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasIndex(e => new { e.ContextId, e.CreatedAtUtc }, "IX_Notes_ContextId_CreatedAtUtc").IsDescending(false, true);

            entity.HasIndex(e => new { e.ContextId, e.Order }, "IX_Notes_ContextId_Order");

            entity.Property(e => e.ArchivedAtUtc).HasPrecision(0);
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Notes_CreatedAtUtc");
            entity.Property(e => e.CreatedByUserId).HasMaxLength(128);
            entity.Property(e => e.ModifiedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Notes_ModifiedAtUtc");
            entity.Property(e => e.ModifiedByUserId).HasMaxLength(128);
            entity.Property(e => e.NoteType).HasMaxLength(20);
            entity.Property(e => e.OwnerUserId).HasMaxLength(128);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Visibility)
                .HasMaxLength(20)
                .HasDefaultValue("Private", "DF_Notes_Visibility");

            entity.HasOne(d => d.Context).WithMany(p => p.Notes)
                .HasForeignKey(d => d.ContextId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<NoteBlock>(entity =>
        {
            entity.HasIndex(e => new { e.NoteId, e.Position }, "IX_NoteBlocks_NoteId_Position");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_NoteBlocks_CreatedAtUtc");
            entity.Property(e => e.CreatedByUserId).HasMaxLength(128);
            entity.Property(e => e.ModifiedAtUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_NoteBlocks_ModifiedAtUtc");
            entity.Property(e => e.ModifiedByUserId).HasMaxLength(128);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.Note).WithMany(p => p.NoteBlocks).HasForeignKey(d => d.NoteId);
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
