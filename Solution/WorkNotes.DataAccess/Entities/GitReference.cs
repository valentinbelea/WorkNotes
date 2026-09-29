using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class GitReference
{
    public int Id { get; set; }

    public string Provider { get; set; } = null!;

    public string RepositoryExternalId { get; set; } = null!;

    public string RepositoryFullName { get; set; } = null!;

    public string RepositoryUrl { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public string Name { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public virtual ICollection<NoteBlockGitReference> NoteBlockGitReferences { get; set; } = new List<NoteBlockGitReference>();
}
