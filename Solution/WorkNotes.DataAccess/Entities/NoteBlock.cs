using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class NoteBlock
{
    public Guid Id { get; set; }

    public int NoteId { get; set; }

    public int Position { get; set; }

    public string Content { get; set; } = null!;

    public DateOnly? ActivityDate { get; set; }

    public bool IsImportant { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public DateTime ModifiedAtUtc { get; set; }

    public string ModifiedByUserId { get; set; } = null!;

    public byte[] RowVersion { get; set; } = null!;

    public virtual Note Note { get; set; } = null!;
}
