using WorkNotes.Business.Models;

namespace WorkNotes.Web.Notes;

// Presentation classes for board cards: the colour follows the note type, and a small tilt is chosen
// from the note's id so it never changes between reloads and needs no inline style.
public static class NoteCardStyle
{
    public const int TiltVariants = 8;

    public static string TypeClass(string noteType) =>
        noteType == NoteTypes.Article ? "note-card--article" : "note-card--journal";

    public static string TiltClass(int noteId) => $"note-card--tilt-{Mix(noteId) % TiltVariants}";

    // Integer hash: consecutive ids land on unrelated variants.
    private static uint Mix(int id)
    {
        unchecked
        {
            var value = (uint)id;
            value ^= value >> 16;
            value *= 0x7feb352d;
            value ^= value >> 15;
            value *= 0x846ca68b;
            value ^= value >> 16;
            return value;
        }
    }
}
