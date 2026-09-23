namespace WorkNotes.Web.Navigation;

// Maps the current Razor page to its main-menu section; the header subtitle and the menu share this rule.
public static class NavigationSections
{
    public static bool IsAccountPage(string? page) =>
        page?.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase) == true;

    public static string? GetSectionKey(string? page) => page switch
    {
        "/Index" => "Navigation_MySpace",
        _ when IsAccountPage(page) => "Navigation_MyAccount",
        _ => null
    };
}
