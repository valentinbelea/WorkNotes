using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class Note
{
    public int Id { get; set; }

    public int ContextId { get; set; }

    public string OwnerUserId { get; set; } = null!;

    public string NoteType { get; set; } = null!;

    public string? Title { get; set; }

    public DateOnly? JournalDate { get; set; }

    public string Visibility { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public DateTime ModifiedAtUtc { get; set; }

    public string ModifiedByUserId { get; set; } = null!;

    public DateTime? ArchivedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual WorkContext Context { get; set; } = null!;

    public virtual ICollection<NoteBlock> NoteBlocks { get; set; } = new List<NoteBlock>();
}
