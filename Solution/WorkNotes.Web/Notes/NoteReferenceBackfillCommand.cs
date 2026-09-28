using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Web.Notes;

// Maintenance command, run from Solution instead of the site:
//   dotnet run --project WorkNotes.Web --launch-profile http -- create-note-references [--save]
// Creates in the existing notes the references their owners would have been offered in the editor
// (INoteReferenceBackfillService). Without --save it only lists what would change. The report is for whoever maintains
// the application, like a log, so it is not localized.
public sealed class NoteReferenceBackfillCommand(INoteReferenceBackfillService backfill)
{
    public const string Name = "create-note-references";
    public const string SaveOption = "--save";

    public static bool IsRequested(IEnumerable<string> args) => args.Contains(Name, StringComparer.Ordinal);

    // The exit code: 0 once every note was read, 1 when the command was stopped first.
    public async Task<int> RunAsync(bool save, TextWriter output, CancellationToken cancellationToken)
    {
        await output.WriteLineAsync(save
            ? "Creating references in the existing notes."
            : $"Preview: nothing is saved. To save the references listed, run the command again with {SaveOption}.");
        int notesRead = 0, notesLinked = 0, references = 0, places = 0, leftAsTheyAre = 0, notSaved = 0;
        try
        {
            await foreach (var note in backfill.CreateReferencesAsync(save, cancellationToken))
            {
                notesRead++;
                if (note.Matches.Count == 0) continue;
                await output.WriteLineAsync($"Note {note.NoteId} {Quoted(note.Title)}");
                foreach (var match in note.Matches)
                {
                    if (match.Targets.Count == 1)
                    {
                        await output.WriteLineAsync($"  {match.Number} -> note {match.Targets[0].Id} {Quoted(match.Targets[0].Title)}{Times(match.Count)}");
                        continue;
                    }
                    leftAsTheyAre++;
                    await output.WriteLineAsync(
                        $"  {match.Number}{Times(match.Count)}: left as it is, notes {string.Join(", ", match.Targets.Select(target => target.Id))} all have it in their title");
                }
                switch (note.Status)
                {
                    case NoteReferenceBackfillStatus.Linked:
                        notesLinked++;
                        var linked = note.Matches.Where(match => match.Targets.Count == 1).ToList();
                        references += linked.Count;
                        places += linked.Sum(match => match.Count);
                        break;
                    case NoteReferenceBackfillStatus.Conflict:
                        notSaved++;
                        await output.WriteLineAsync("  Not saved: the note changed while it was read. Run the command again for it.");
                        break;
                    case NoteReferenceBackfillStatus.TooLong:
                        notSaved++;
                        await output.WriteLineAsync("  Not saved: with the references its text would be longer than a note may be.");
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await output.WriteLineAsync($"Stopped. Notes read: {notesRead}. Each note is saved whole or not at all; running the command again goes on with the rest.");
            return 1;
        }
        await output.WriteLineAsync($"Notes read: {notesRead}. {(save ? "Notes changed" : "Notes to change")}: {notesLinked}, with {references} references in {places} places. "
            + $"Numbers left as they are because several notes have them: {leftAsTheyAre}. Notes not saved: {notSaved}.");
        return 0;
    }

    private static string Quoted(string? title) => title is null ? "(untitled)" : $"\"{title}\"";

    private static string Times(int count) => count > 1 ? $" (x{count})" : "";
}
