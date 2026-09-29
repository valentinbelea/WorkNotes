using Microsoft.Extensions.Localization;
using WorkNotes.Business.Models;

namespace WorkNotes.Web.Notes;

// The Git references of a note as the references drawer shows them (the branches linked to its references). What is
// linked comes from the service (INoteGitReferenceService); nothing here reads references in the text.
public static class NoteGitReferences
{
    // The reference as people write it: CR:30080 reads CR 30080.
    public static string Label(string normalizedReference) => normalizedReference.Replace(':', ' ');

    // The name of the button that removes a link, for screen readers.
    public static string RemoveLabel(IStringLocalizer localizer, NoteGitReference reference) =>
        localizer["Editor_GitReferenceRemoveNamed", Label(reference.NormalizedReference), reference.Name].Value;

    // The links for note-editor.js (the page's data and the answers of adding and removing): each with the texts it shows.
    public static IEnumerable<object> Data(IStringLocalizer localizer, IReadOnlyList<NoteGitReference>? references) =>
        (references ?? []).Select(reference => new
        {
            id = reference.Id,
            reference = Label(reference.NormalizedReference),
            repository = reference.RepositoryFullName,
            branch = reference.Name,
            url = reference.Url,
            remove = RemoveLabel(localizer, reference)
        });
}
