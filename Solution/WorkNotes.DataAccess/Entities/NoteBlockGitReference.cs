using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class NoteBlockGitReference
{
    public int Id { get; set; }

    public Guid NoteBlockId { get; set; }

    public int GitReferenceId { get; set; }

    public int WorkReferenceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public virtual GitReference GitReference { get; set; } = null!;

    public virtual NoteBlock NoteBlock { get; set; } = null!;

    public virtual WorkReference WorkReference { get; set; } = null!;
}
