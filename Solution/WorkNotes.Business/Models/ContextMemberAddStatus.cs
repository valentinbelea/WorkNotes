namespace WorkNotes.Business.Models;

// Stable result codes; the presentation layer maps them to localized messages.
public enum ContextMemberAddStatus
{
    Added,
    NotFound,
    Forbidden,
    InvalidEmail,
    UserNotFound,
    AlreadyMember
}
