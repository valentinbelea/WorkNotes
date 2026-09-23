using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class WorkContext
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }
}
