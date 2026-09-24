using System.Globalization;
using Microsoft.Extensions.Localization;

namespace WorkNotes.Web.Notes;

// Dates shown on the board and in the editor, in the application's local time, so both read the same.
public static class NoteDates
{
    public static string Iso(DateTime utc) => utc.ToLocalTime().ToString("yyyy-MM-ddTHH:mmzzz", CultureInfo.InvariantCulture);

    public static string Card(DateTime utc) => utc.ToLocalTime().ToString("dd.MM.yyyy · HH:mm", CultureInfo.InvariantCulture);

    public static string Full(DateTime utc) => utc.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    // The editor's info bar: when a paragraph was created and last changed (NoteBlocks audit columns).
    // The unsaved variant is shown while the paragraph's text differs from what was stored.
    public static string BlockAudit(IStringLocalizer localizer, DateTime createdAtUtc, DateTime modifiedAtUtc, bool unsaved = false) =>
        (modifiedAtUtc > createdAtUtc, unsaved) switch
        {
            (true, false) => localizer["Editor_BlockAudit", Full(createdAtUtc), Full(modifiedAtUtc)].Value,
            (true, true) => localizer["Editor_BlockAuditUnsaved", Full(createdAtUtc), Full(modifiedAtUtc)].Value,
            (false, false) => localizer["Editor_BlockAuditUnchanged", Full(createdAtUtc)].Value,
            (false, true) => localizer["Editor_BlockAuditUnchangedUnsaved", Full(createdAtUtc)].Value
        };
}
