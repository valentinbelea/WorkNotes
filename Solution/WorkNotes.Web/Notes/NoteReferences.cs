using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using WorkNotes.Business.Models;

namespace WorkNotes.Web.Notes;

// Internal references as the editor shows them: where a paragraph shows links and the notes they open. Where the links
// are comes from the service (INoteReferenceService); nothing here reads references in the text.
public static class NoteReferences
{
    public static string TypeName(IStringLocalizer localizer, string noteType) =>
        localizer[noteType == NoteTypes.Article ? "NoteType_Article" : "NoteType_Journal"].Value;

    public static string Title(IStringLocalizer localizer, NoteReferenceTarget target) =>
        target.Title ?? localizer["Notes_Untitled"].Value;

    // The tooltip of a link: the title and type of the note it opens, as they are now.
    public static string Label(IStringLocalizer localizer, NoteReferenceTarget target) =>
        localizer["Notes_ReferenceTarget", Title(localizer, target), TypeName(localizer, target.NoteType)].Value;

    // A paragraph's links for note-editor.js: from and to count characters of the paragraph's text.
    public static IEnumerable<object> Links(IReadOnlyList<NoteReferenceLink>? links) =>
        (links ?? []).Select(link => new { from = link.Start, to = link.Start + link.Length, note = link.TargetNoteId });

    // The notes the links open, each with its tooltip.
    public static IEnumerable<object> Targets(IStringLocalizer localizer, IReadOnlyList<NoteReferenceTarget>? targets) =>
        (targets ?? []).Select(target => new { id = target.Id, label = Label(localizer, target) });

    // A paragraph as HTML for reading without JavaScript: the text is encoded; each link is a link to its note
    // (/?note={id}). Nothing else is added, so the text keeps its own spacing and line breaks.
    public static IHtmlContent Html(NoteBlockDetails block, IReadOnlyList<NoteReferenceTarget>? targets, IStringLocalizer localizer, IUrlHelper url)
    {
        var content = new HtmlContentBuilder();
        var known = (targets ?? []).ToDictionary(target => target.Id);
        var text = block.Content;
        var position = 0;
        foreach (var link in (block.Links ?? []).OrderBy(link => link.Start))
        {
            // Links come in text order and never overlap; one that does not fit the text is left out.
            if (link.Start < position || link.Length <= 0 || link.Start + link.Length > text.Length
                || !known.TryGetValue(link.TargetNoteId, out var target)) continue;
            content.Append(text[position..link.Start]);
            var anchor = new TagBuilder("a");
            anchor.Attributes["href"] = url.Page("/Index", new { note = link.TargetNoteId });
            anchor.Attributes["data-note-reference"] = link.TargetNoteId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            anchor.Attributes["title"] = Label(localizer, target);
            anchor.AddCssClass("note-reference");
            anchor.InnerHtml.Append(text.Substring(link.Start, link.Length));
            content.AppendHtml(anchor);
            position = link.Start + link.Length;
        }
        content.Append(text[position..]);
        return content;
    }
}
