namespace WorkNotes.Business.Models;

public static class GitHubEnvironments
{
    public const string Development = "Development";
    public const string Production = "Production";

    public static bool IsSupported(string environmentName) =>
        environmentName is Development or Production;
}
