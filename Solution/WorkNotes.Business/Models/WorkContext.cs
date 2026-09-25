namespace WorkNotes.Business.Models;

// IsOwner describes the current user's role, so the same context reads differently for each member.
public sealed record WorkContext(int Id, string Name, string? Description, bool IsOwner);
