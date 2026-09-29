namespace WorkNotes.Business.Models;

// A branch as the editor offers it: its name and the address of its page at the provider.
public sealed record GitBranchInfo(string Name, string Url);
