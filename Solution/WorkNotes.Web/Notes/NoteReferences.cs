using System.Globalization;
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

    // A paragraph's links for note-editor.js: from and to count characters of the paragraph's text; notes are the notes
    // the link opens, in order.
    public static IEnumerable<object> Links(IReadOnlyList<NoteReferenceLink>? links) =>
        (links ?? []).Select(link => new { from = link.Start, to = link.Start + link.Length, notes = link.TargetNoteIds, reference = link.NormalizedReference });

    // The notes the links open, each with its tooltip.
    public static IEnumerable<object> Targets(IStringLocalizer localizer, IReadOnlyList<NoteReferenceTarget>? targets) =>
        (targets ?? []).Select(target => new { id = target.Id, label = Label(localizer, target) });

    // One entry of the references drawer: a reference of the note, as its first link writes it (CR_30080), and the notes
    // it opens.
    public sealed record ListEntry(string Label, IReadOnlyList<NoteReferenceTarget> Notes);

    // The references drawer of a note: each reference its paragraphs show as a link, once, in the order it first appears
    // in the text and named as it is written there, with all the notes its links open (in the order of their ids) among
    // the notes given. The paragraphs' links come in document order.
    public static IReadOnlyList<ListEntry> List(IEnumerable<IReadOnlyList<NoteReferenceLink>?> paragraphs, IReadOnlyList<NoteReferenceTarget>? targets)
    {
        var known = (targets ?? []).ToDictionary(target => target.Id);
        var entries = new List<(string Label, SortedSet<int> Notes)>();
        var byReference = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);
        foreach (var links in paragraphs)
        {
            foreach (var link in (links ?? []).OrderBy(link => link.Start))
            {
                if (!byReference.TryGetValue(link.NormalizedReference, out var notes))
                {
                    byReference[link.NormalizedReference] = notes = [];
                    entries.Add((link.Text, notes));
                }
                notes.UnionWith(link.TargetNoteIds.Where(known.ContainsKey));
            }
        }
        return entries
            .Where(entry => entry.Notes.Count > 0)
            .Select(entry => new ListEntry(entry.Label, entry.Notes.Select(id => known[id]).ToList()))
            .ToList();
    }

    // The references drawer in the answer of a save: each entry's name and the ids of its notes.
    public static IEnumerable<object> ListData(IReadOnlyList<ListEntry> entries) =>
        entries.Select(entry => new { label = entry.Label, notes = entry.Notes.Select(target => target.Id) });

    // A paragraph as HTML for reading without JavaScript: the text is encoded; each link is a link to its first note
    // (/?note={id}), and each other note of the link follows it as a small numbered link (2, 3...), since a link without
    // JavaScript opens a single note. Nothing else is added, so the text keeps its own spacing and line breaks.
    public static IHtmlContent Html(NoteBlockDetails block, IReadOnlyList<NoteReferenceTarget>? targets, IStringLocalizer localizer, IUrlHelper url)
    {
        var content = new HtmlContentBuilder();
        var known = (targets ?? []).ToDictionary(target => target.Id);
        var text = block.Content;
        var position = 0;
        foreach (var link in (block.Links ?? []).OrderBy(link => link.Start))
        {
            var notes = link.TargetNoteIds.Where(known.ContainsKey).Select(id => known[id]).ToList();
            // Links come in text order and never overlap; one that does not fit the text is left out.
            if (link.Start < position || link.Length <= 0 || link.Start + link.Length > text.Length || notes.Count == 0) continue;
            content.Append(text[position..link.Start]);
            content.AppendHtml(Anchor(notes[0], text.Substring(link.Start, link.Length), localizer, url));
            for (var index = 1; index < notes.Count; index++)
            {
                var more = new TagBuilder("sup");
                more.AddCssClass("note-reference-more");
                var anchor = Anchor(notes[index], (index + 1).ToString(CultureInfo.InvariantCulture), localizer, url);
                // The number alone says nothing: the note's title and type are the link's name.
                anchor.Attributes["aria-label"] = Label(localizer, notes[index]);
                more.InnerHtml.AppendHtml(anchor);
                content.AppendHtml(more);
            }
            position = link.Start + link.Length;
        }
        content.Append(text[position..]);
        return content;
    }

    private static TagBuilder Anchor(NoteReferenceTarget target, string text, IStringLocalizer localizer, IUrlHelper url)
    {
        var anchor = new TagBuilder("a");
        anchor.Attributes["href"] = url.Page("/Index", new { note = target.Id });
        anchor.Attributes["data-note-reference"] = target.Id.ToString(CultureInfo.InvariantCulture);
        anchor.Attributes["title"] = Label(localizer, target);
        anchor.AddCssClass("note-reference");
        anchor.InnerHtml.Append(text);
        return anchor;
    }
}
