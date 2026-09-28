namespace WorkNotes.Business.Abstractions;

// Data access for the reference types (dbo.ReferenceTypes): the prefixes a reference is written with (CR, BUG...).
public interface IReferenceTypeRepository
{
    // The codes of the active types, as stored (capital letters), in code order. Empty when none is active.
    Task<IReadOnlyList<string>> GetActiveTypesAsync(CancellationToken cancellationToken);
}
