using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Business.Abstractions;
namespace WorkNotes.Web.Pages.Account;

public sealed class LogoutModel(IAuthenticationService authentication) : PageModel
{
    public IActionResult OnGet() => StatusCode(StatusCodes.Status405MethodNotAllowed);
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await authentication.SignOutAsync(cancellationToken);
        return RedirectToPage("/Index");
    }
}

