using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Web.Localization;

namespace WorkNotes.Web.Pages;

[AllowAnonymous]
public sealed class LanguageModel : PageModel
{
    public IActionResult OnGet() => StatusCode(StatusCodes.Status405MethodNotAllowed);

    public IActionResult OnPost(string culture, string returnUrl)
    {
        if (!LocalizationConfiguration.SupportedCultures.Contains(culture) || !Url.IsLocalUrl(returnUrl))
            return BadRequest();
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = true,
                IsEssential = true, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps,
                Path = Request.PathBase.HasValue ? Request.PathBase.Value : "/" });
        // Preserve pending success messages while changing their presentation language.
        TempData.Keep();
        return LocalRedirect(returnUrl);
    }
}

