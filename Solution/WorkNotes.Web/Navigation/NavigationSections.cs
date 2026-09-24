namespace WorkNotes.Web.Navigation;

// Maps the current Razor page to its main-menu section; the header subtitle and the menu share this rule.
public static class NavigationSections
{
    public static bool IsAccountPage(string? page) =>
        page?.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase) == true;

    public static string? GetSectionKey(string? page) => page switch
    {
        "/Index" or "/Notes/Edit" => "Navigation_MySpace",
        _ when page?.StartsWith("/Contexts/", StringComparison.OrdinalIgnoreCase) == true => "Navigation_Contexts",
        _ when IsAccountPage(page) => "Navigation_MyAccount",
        _ => null
    };
}
