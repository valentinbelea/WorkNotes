using System;
using System.Collections.Generic;

namespace WorkNotes.DataAccess.Entities;

public partial class GitConnection
{
    public string UserId { get; set; } = null!;

    public string Provider { get; set; } = null!;

    public string AccountId { get; set; } = null!;

    public string AccountLogin { get; set; } = null!;

    public string ProtectedAccessToken { get; set; } = null!;

    public string? ProtectedRefreshToken { get; set; }

    public DateTime? AccessTokenExpiresAtUtc { get; set; }

    public DateTime? RefreshTokenExpiresAtUtc { get; set; }

    public string? Scopes { get; set; }

    public DateTime ConnectedAtUtc { get; set; }

    public DateTime ValidatedAtUtc { get; set; }
}
