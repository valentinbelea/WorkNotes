namespace WorkNotes.DataAccess.Entities;

public sealed class GitHubConfiguration
{
    public int Id { get; set; }
    public string ProtectedClientId { get; set; } = null!;
    public string ProtectedClientSecret { get; set; } = null!;
    public string Scopes { get; set; } = null!;
    public string CallbackUrl { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
