using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class NoteReferenceTarget
{
    public int NoteReferenceId { get; set; }

    public int TargetNoteId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual NoteReference NoteReference { get; set; } = null!;

    public virtual Note TargetNote { get; set; } = null!;
}
