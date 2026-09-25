using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using WorkNotes.Business.Models;

namespace WorkNotes.Web.Notes;

// References between notes as the board and the editor show them (NoteReferenceRules holds their stored form).
public static class NoteReferences
{
    public static string TypeName(IStringLocalizer localizer, string noteType) =>
        localizer[noteType == NoteTypes.Article ? "NoteType_Article" : "NoteType_Journal"].Value;

    public static string Title(IStringLocalizer localizer, NoteReferenceTarget target) =>
        target.Title ?? localizer["Notes_Untitled"].Value;

    // The tooltip of a reference: the title and type of the note it opens, as they are now.
    public static string Label(IStringLocalizer localizer, NoteReferenceTarget target) =>
        localizer["Notes_ReferenceTarget", Title(localizer, target), TypeName(localizer, target.NoteType)].Value;

    // A note's text as HTML: the text is encoded; a reference whose note the reader may open is a link to it
    // (/?note={id}, which notes-board.js opens in the editor when the editor is on the page), any other one keeps its
    // number, marked as a reference that can no longer be opened. Nothing else is added, so the text keeps its own
    // spacing and line breaks.
    public static IHtmlContent Html(string? text, IReadOnlyList<NoteReferenceTarget>? targets, IStringLocalizer localizer, IUrlHelper url)
    {
        var content = new HtmlContentBuilder();
        var known = (targets ?? []).ToDictionary(target => target.Id);
        foreach (var part in NoteReferenceRules.Split(text))
        {
            if (part.TargetNoteId is not { } targetNoteId)
            {
                content.Append(part.Text);
                continue;
            }
            TagBuilder reference;
            if (known.TryGetValue(targetNoteId, out var target))
            {
                reference = new TagBuilder("a");
                reference.Attributes["href"] = url.Page("/Index", new { note = targetNoteId });
                reference.Attributes["data-note-reference"] = targetNoteId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                reference.Attributes["title"] = Label(localizer, target);
                reference.AddCssClass("note-reference");
            }
            else
            {
                reference = new TagBuilder("span");
                reference.Attributes["title"] = localizer["Notes_ReferenceBroken"].Value;
                reference.AddCssClass("note-reference note-reference--broken");
            }
            reference.InnerHtml.Append(part.Text);
            content.AppendHtml(reference);
        }
        return content;
    }
}
