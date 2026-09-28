using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Business.Services;

public sealed class ReferenceTypeService(IReferenceTypeRepository types, ReferenceTypeCache cache, TimeProvider time) : IReferenceTypeService
{
    // The parser this request reads with: once taken, the same for all its texts.
    private NoteReferenceParser? parser;

    public async Task<NoteReferenceParser> GetParserAsync(CancellationToken cancellationToken)
    {
        if (parser is not null) return parser;
        if (cache.Current is { } current && time.GetElapsedTime(current.ReadAt) < ReferenceTypeCache.Duration)
            return parser = current.Parser;

        cancellationToken.ThrowIfCancellationRequested();
        // Taken before the read: what is read is at least that recent.
        var readAt = time.GetTimestamp();
        var read = new NoteReferenceParser(await types.GetActiveTypesAsync(cancellationToken));
        cache.Current = new ReferenceTypeCache.Entry(read, readAt);
        return parser = read;
    }
}
