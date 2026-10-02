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
public sealed class GitHubModel(IGitHubConnectionService gitHub, GitHubAuthorizationCookie authorizationCookie,
    IAccountService accounts, IGitHubConfigurationService configuration,
    ILogger<GitHubModel> logger) : PageModel
{
    public bool IsConfigured { get; private set; }
    public bool HasEmail { get; private set; }
    public GitConnection? Connection { get; private set; }
    public string EnvironmentName => configuration.CurrentEnvironmentName;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        HasEmail = await HasUserEmailAsync(cancellationToken);
        IsConfigured = await gitHub.IsConfiguredAsync(cancellationToken);
        if (!IsConfigured)
            logger.LogWarning("GitHub configuration missing for environment: {EnvironmentName}", EnvironmentName);
        if (HasEmail) Connection = await gitHub.GetAsync(UserId, cancellationToken);
    }

    public async Task<IActionResult> OnPostConnectAsync(CancellationToken cancellationToken)
    {
        if (!await HasUserEmailAsync(cancellationToken)) return Message("GitHub_EmailRequired", StatusMessageKind.Error);
        var authorization = await gitHub.StartAuthorizationAsync(cancellationToken);
        if (authorization is null) return Message("GitHub_NotConfiguredForEnvironment", StatusMessageKind.Error, EnvironmentName);
        authorizationCookie.Write(HttpContext, UserId, authorization.Pending);
        return Redirect(authorization.Url);
    }

    // A GET by the OAuth protocol: the state in the protected cookie, not antiforgery, ties it to the Connect above.
    public async Task<IActionResult> OnGetCallbackAsync(string? code, string? state, string? error,
        CancellationToken cancellationToken)
    {
        if (!await HasUserEmailAsync(cancellationToken)) return Message("GitHub_EmailRequired", StatusMessageKind.Error);
        var pending = authorizationCookie.Take(HttpContext, UserId);
        var status = await gitHub.CompleteAuthorizationAsync(UserId, pending, new GitHubCallback(code, state, error),
            cancellationToken);
        return status switch
        {
            GitConnectStatus.Connected => Message("GitHub_Connected"),
            GitConnectStatus.NotConfigured => Message("GitHub_NotConfiguredForEnvironment", StatusMessageKind.Error, EnvironmentName),
            GitConnectStatus.InvalidState => Message("GitHub_InvalidState", StatusMessageKind.Error),
            GitConnectStatus.Denied => Message("GitHub_Denied", StatusMessageKind.Warning),
            GitConnectStatus.Rejected => Message("GitHub_Rejected", StatusMessageKind.Error),
            _ => Message("GitHub_Unavailable", StatusMessageKind.Error)
        };
    }

    public async Task<IActionResult> OnPostVerifyAsync(CancellationToken cancellationToken) =>
        !await HasUserEmailAsync(cancellationToken)
            ? Message("GitHub_EmailRequired", StatusMessageKind.Error)
            : await gitHub.VerifyAsync(UserId, cancellationToken) switch
        {
            GitVerifyStatus.Valid => Message("GitHub_Valid"),
            GitVerifyStatus.NotConnected => Message("GitHub_NotConnected", StatusMessageKind.Warning),
            GitVerifyStatus.NotConfigured => Message("GitHub_NotConfiguredForEnvironment", StatusMessageKind.Error, EnvironmentName),
            GitVerifyStatus.ReconnectRequired => Message("GitHub_ReconnectRequired", StatusMessageKind.Warning),
            _ => Message("GitHub_Unavailable", StatusMessageKind.Error)
        };

    public async Task<IActionResult> OnPostDisconnectAsync(CancellationToken cancellationToken) =>
        !await HasUserEmailAsync(cancellationToken)
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

    private async Task<bool> HasUserEmailAsync(CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            email = (await accounts.GetProfileAsync(UserId, cancellationToken))?.Email;
        return !string.IsNullOrWhiteSpace(email?.Trim().ToLowerInvariant());
    }

    private RedirectToPageResult Message(string key, StatusMessageKind kind = StatusMessageKind.Success, string? argument = null)
    {
        if (argument is null) TempData.SetStatusMessage(key, kind);
        else TempData.SetStatusMessage(key, kind, argument);
        return RedirectToPage();
    }
}
