using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class ContextMember
{
    public int ContextId { get; set; }

    public string UserId { get; set; } = null!;

    public string Role { get; set; } = null!;

    public DateTime AddedAtUtc { get; set; }

    public virtual WorkContext Context { get; set; } = null!;
}
