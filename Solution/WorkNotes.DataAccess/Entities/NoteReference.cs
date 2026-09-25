using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class NoteReference
{
    public int Id { get; set; }

    public int SourceNoteId { get; set; }

    public int TargetNoteId { get; set; }

    public string DisplayText { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public virtual Note SourceNote { get; set; } = null!;

    public virtual Note TargetNote { get; set; } = null!;
}
