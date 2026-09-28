using WorkNotes.Business.Models;

namespace WorkNotes.Business.Abstractions;

// Creates in the notes written before references existed the references their owners would have been offered.
public interface INoteReferenceBackfillService
{
    // Reads every note its owner can edit and turns each number the editor would offer a reference for
    // (NoteReferenceRules.LinkNumbers) into a reference when exactly one other note of the board the owner may see has
    // it whole in its title. A number several notes have stays as it is. The text reads the same, so the audit dates
    // and the order on the board do not change. With save false nothing is stored: a preview. One result per note read,
    // as soon as it is done.
    IAsyncEnumerable<NoteReferenceBackfillNote> CreateReferencesAsync(bool save, CancellationToken cancellationToken);
}
