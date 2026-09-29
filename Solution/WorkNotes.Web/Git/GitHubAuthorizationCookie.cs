using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using WorkNotes.Business.Models;

namespace WorkNotes.Web.Git;

// Keeps the pending GitHub authorization (state and PKCE verifier) in the browser between leaving for GitHub and the
// callback. The value is encrypted and signed with Data Protection for this user only (the user ID is part of the
// protector's purpose) and expires after Lifetime; the cookie is HttpOnly, limited to /Account/GitHub and read once.
public sealed class GitHubAuthorizationCookie(IDataProtectionProvider protection, IHostEnvironment environment, TimeProvider time)
{
    public const string Name = "WorkNotes.GitHubAuthorization";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private const string Path = "/Account/GitHub";

    public void Write(HttpContext context, string userId, GitHubPendingAuthorization pending)
    {
        var value = Protector(userId).Protect(JsonSerializer.Serialize(pending), time.GetUtcNow() + Lifetime);
        context.Response.Cookies.Append(Name, value, Options(context, time.GetUtcNow() + Lifetime));
    }

    // Null when the cookie is missing, expired, altered or written for another user; it is deleted either way.
    public GitHubPendingAuthorization? Take(HttpContext context, string userId)
    {
        if (!context.Request.Cookies.TryGetValue(Name, out var value)) return null;
        context.Response.Cookies.Delete(Name, Options(context, null));
        try
        {
            return JsonSerializer.Deserialize<GitHubPendingAuthorization>(Protector(userId).Unprotect(value));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return null;
        }
    }

    private ITimeLimitedDataProtector Protector(string userId) =>
        protection.CreateProtector("WorkNotes.GitHubAuthorization", userId).ToTimeLimitedDataProtector();

    private CookieOptions Options(HttpContext context, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        // Lax: the cookie comes back with GitHub's top-level redirect to the callback.
        SameSite = SameSiteMode.Lax,
        Secure = !environment.IsDevelopment() || context.Request.IsHttps,
        Path = context.Request.PathBase.Add(Path),
        IsEssential = true,
        Expires = expires
    };
}
