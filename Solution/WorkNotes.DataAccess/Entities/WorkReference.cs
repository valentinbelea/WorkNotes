using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class WorkReference
{
    public int Id { get; set; }

    public string ReferenceType { get; set; } = null!;

    public long ReferenceNumber { get; set; }

    public string NormalizedReference { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public virtual ICollection<NoteReference> NoteReferences { get; set; } = new List<NoteReference>();

    public virtual ReferenceType ReferenceTypeNavigation { get; set; } = null!;
}
