using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WorkNotes.Business.Abstractions;
using WorkNotes.Resources.Resources;
using WorkNotes.Web.Authentication;
using WorkNotes.Web.ViewModels;

namespace WorkNotes.Web.Pages.Admin;

[AllowAnonymous]
public sealed class LoginModel(IAdminAuthenticationService authentication, IStringLocalizer<SharedResources> localizer) : PageModel
{
    [BindProperty] public AdminLoginInput Input { get; set; } = new();

    public IActionResult OnGet() => User.Identities.Any(x => x.AuthenticationType == AdminAuthenticationDefaults.Scheme)
        ? RedirectToPage("/Admin/Index") : Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var admin = await authentication.AuthenticateAsync(Input.UserName, Input.Password, cancellationToken);
        if (admin is null)
        {
            ModelState.AddModelError("", localizer["Message_AdminLoginFailed"]);
            return Page();
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()), new Claim(ClaimTypes.Name, admin.UserName)
        ], AdminAuthenticationDefaults.Scheme));
        await HttpContext.SignInAsync(AdminAuthenticationDefaults.Scheme, principal);
        return RedirectToPage("/Admin/Index");
    }
}
