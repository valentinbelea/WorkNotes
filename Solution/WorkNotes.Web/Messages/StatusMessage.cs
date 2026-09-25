namespace WorkNotes.Web.Messages;

// A save result shown by Pages/Shared/_StatusMessage.cshtml. Key is the resource key of its text;
// it is null in the templates that notes-board.js fills in.
public sealed record StatusMessage(StatusMessageKind Kind, string? Key = null);
