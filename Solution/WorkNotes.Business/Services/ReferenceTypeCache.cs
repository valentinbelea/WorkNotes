using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

// The parser of the reference types, shared by every request: one instance for the application (a singleton). The types
// are the same for every user and change rarely, so they are read again only once Duration has passed since they were
// read. It holds data only, no dependency: ReferenceTypeService, in each request, reads the table and fills it.
public sealed class ReferenceTypeCache
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);

    private Entry? current;

    // The parser last read and when (a TimeProvider timestamp); null until the first read.
    public Entry? Current
    {
        get => Volatile.Read(ref current);
        set => Volatile.Write(ref current, value);
    }

    public sealed record Entry(NoteReferenceParser Parser, long ReadAt);
}
