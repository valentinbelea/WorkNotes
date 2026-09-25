namespace WorkNotes.Business.Models;

// Role codes stored in ContextMembers.Role; the database CHECK constraint accepts only these values.
public static class ContextRoles
{
    public const string Owner = "Owner";
    public const string Member = "Member";
}
