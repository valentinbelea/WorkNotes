using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class GitRepository
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public string Provider { get; set; } = null!;

    public string ExternalId { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsPrivate { get; set; }

    public string? DefaultBranch { get; set; }

    public string HtmlUrl { get; set; } = null!;

    public DateTime ImportedAtUtc { get; set; }

    public DateTime RefreshedAtUtc { get; set; }
}
