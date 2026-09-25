namespace WorkNotes.Web.Messages;

// How a save result reads: it chooses the message's colour, icon and the kind named to screen readers.
public enum StatusMessageKind
{
    Success,
    Warning,
    Error
}
