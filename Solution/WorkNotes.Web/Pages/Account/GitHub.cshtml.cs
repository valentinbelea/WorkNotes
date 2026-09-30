using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;
using WorkNotes.Web.Git;
using WorkNotes.Web.Messages;

namespace WorkNotes.Web.Pages.Account;

// The user's GitHub connection: /Account/GitHub shows it, Connect sends the browser to GitHub, GitHub sends it back to
// /Account/GitHub/Callback (the callback URL registered on GitHub), Verify and Disconnect change it.
[Authorize]
public sealed class GitHubModel(IGitHubConnectionService gitHub, GitHubAuthorizationCookie authorizationCookie) : PageModel
{
    public bool IsConfigured => gitHub.IsConfigured;
    public bool HasEmail => UserEmail is not null;
    public GitConnection? Connection { get; private set; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string? UserEmail => User.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant() is { Length: > 0 } email
        ? email
        : null;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (HasEmail) Connection = await gitHub.GetAsync(UserId, cancellationToken);
    }

    public IActionResult OnPostConnect()
    {
        if (!HasEmail) return Message("GitHub_EmailRequired", StatusMessageKind.Error);
        if (!gitHub.IsConfigured) return Message("GitHub_NotConfigured", StatusMessageKind.Error);

        var authorization = gitHub.StartAuthorization(CallbackUrl());
        authorizationCookie.Write(HttpContext, UserId, authorization.Pending);
        return Redirect(authorization.Url);
    }

    // A GET by the OAuth protocol: the state in the protected cookie, not antiforgery, ties it to the Connect above.
    public async Task<IActionResult> OnGetCallbackAsync(string? code, string? state, string? error,
        CancellationToken cancellationToken)
    {
        if (!HasEmail) return Message("GitHub_EmailRequired", StatusMessageKind.Error);
        var pending = authorizationCookie.Take(HttpContext, UserId);
        var status = await gitHub.CompleteAuthorizationAsync(UserId, pending, new GitHubCallback(code, state, error),
            CallbackUrl(), cancellationToken);
        return status switch
        {
            GitConnectStatus.Connected => Message("GitHub_Connected"),
            GitConnectStatus.NotConfigured => Message("GitHub_NotConfigured", StatusMessageKind.Error),
            GitConnectStatus.InvalidState => Message("GitHub_InvalidState", StatusMessageKind.Error),
            GitConnectStatus.Denied => Message("GitHub_Denied", StatusMessageKind.Warning),
            GitConnectStatus.Rejected => Message("GitHub_Rejected", StatusMessageKind.Error),
            _ => Message("GitHub_Unavailable", StatusMessageKind.Error)
        };
    }

    public async Task<IActionResult> OnPostVerifyAsync(CancellationToken cancellationToken) =>
        !HasEmail
            ? Message("GitHub_EmailRequired", StatusMessageKind.Error)
            : await gitHub.VerifyAsync(UserId, cancellationToken) switch
        {
            GitVerifyStatus.Valid => Message("GitHub_Valid"),
            GitVerifyStatus.NotConnected => Message("GitHub_NotConnected", StatusMessageKind.Warning),
            GitVerifyStatus.NotConfigured => Message("GitHub_NotConfigured", StatusMessageKind.Error),
            GitVerifyStatus.ReconnectRequired => Message("GitHub_ReconnectRequired", StatusMessageKind.Warning),
            _ => Message("GitHub_Unavailable", StatusMessageKind.Error)
        };

    public async Task<IActionResult> OnPostDisconnectAsync(CancellationToken cancellationToken) =>
        !HasEmail
            ? Message("GitHub_EmailRequired", StatusMessageKind.Error)
            : await gitHub.DisconnectAsync(UserId, cancellationToken) switch
        {
            GitDisconnectStatus.Disconnected => Message("GitHub_Disconnected"),
            GitDisconnectStatus.NotRevoked => Message("GitHub_NotRevoked", StatusMessageKind.Warning),
            _ => Message("GitHub_NotConnected", StatusMessageKind.Warning)
        };

    // The handlers opened directly with GET go back to the page.
    public IActionResult OnGetConnect() => RedirectToPage();
    public IActionResult OnGetVerify() => RedirectToPage();
    public IActionResult OnGetDisconnect() => RedirectToPage();

    private string CallbackUrl() =>
        Url.PageLink("/Account/GitHub", "Callback") ?? throw new InvalidOperationException("No callback URL.");

    private RedirectToPageResult Message(string key, StatusMessageKind kind = StatusMessageKind.Success)
    {
        TempData.SetStatusMessage(key, kind);
        return RedirectToPage();
    }
}
