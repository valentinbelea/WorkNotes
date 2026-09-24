using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace WorkNotes.Web.Messages;

// A status message survives the redirect in TempData as "{kind}|{resource key}": the key, never the translation,
// and its kind, so a warning or an error keeps its look. The layout shows the page message; the members dialog its own.
public static class StatusMessageTempData
{
    public const string Page = "StatusMessage";
    public const string Members = "MembersMessage";

    public static void SetStatusMessage(this ITempDataDictionary tempData, string key,
        StatusMessageKind kind = StatusMessageKind.Success, string entry = Page) =>
        tempData[entry] = $"{kind}|{key}";

    public static StatusMessage? TakeStatusMessage(this ITempDataDictionary tempData, string entry = Page) =>
        tempData[entry] is string value
        && value.Split('|') is [var kind, var key]
        && Enum.TryParse<StatusMessageKind>(kind, out var parsed)
        && Enum.IsDefined(parsed)
            ? new StatusMessage(parsed, key)
            : null;
}
