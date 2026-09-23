using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class WorkContext
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<ContextMember> ContextMembers { get; set; } = new List<ContextMember>();

    public virtual ICollection<Note> Notes { get; set; } = new List<Note>();
}
