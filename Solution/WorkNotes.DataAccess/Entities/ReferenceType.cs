using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class ReferenceType
{
    public string Code { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual ICollection<NoteReference> NoteReferences { get; set; } = new List<NoteReference>();

    public virtual ICollection<WorkReference> WorkReferences { get; set; } = new List<WorkReference>();
}
