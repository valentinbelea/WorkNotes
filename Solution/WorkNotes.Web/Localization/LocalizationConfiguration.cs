using Microsoft.AspNetCore.Localization;

namespace WorkNotes.Web.Localization;

public static class LocalizationConfiguration
{
    public const string DefaultCulture = "ro-RO";
    public static readonly string[] SupportedCultures = ["ro-RO", "en-US", "pl-PL"];

    public static void Configure(RequestLocalizationOptions options)
    {
        options.SetDefaultCulture(DefaultCulture).AddSupportedCultures(SupportedCultures).AddSupportedUICultures(SupportedCultures);
        options.RequestCultureProviders = [new CookieRequestCultureProvider()];
        options.ApplyCurrentCultureToResponseHeaders = true;
    }
}

