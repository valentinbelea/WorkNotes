using Microsoft.Extensions.Localization;
using WorkNotes.Resources.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Business.Abstractions;
using WorkNotes.Web.ViewModels;
namespace WorkNotes.Web.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel(IAuthenticationService authentication, IStringLocalizer<SharedResources> localizer) : PageModel
{
    [BindProperty] public LoginInput Input { get; set; } = new();
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        if (await authentication.SignInAsync(Input.Email, Input.Password, Input.RememberMe, cancellationToken))
            return RedirectToPage("/Index");
        ModelState.AddModelError("", localizer["Message_LoginFailed"]);
        return Page();
    }
}

