using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class NoteReference
{
    public int Id { get; set; }

    public Guid NoteBlockId { get; set; }

    public int TargetNoteId { get; set; }

    public string ReferenceType { get; set; } = null!;

    public long ReferenceNumber { get; set; }

    public string ReferenceText { get; set; } = null!;

    public string NormalizedReference { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public virtual NoteBlock NoteBlock { get; set; } = null!;

    public virtual Note TargetNote { get; set; } = null!;
}
