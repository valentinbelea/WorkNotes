namespace WorkNotes.Business.Models;

public sealed record AccountResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static AccountResult Success() => new(true, []);
    public static AccountResult Failure(params string[] errors) => new(false, errors);
}

