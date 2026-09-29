using System.Security.Cryptography;
using System.Text;

namespace WorkNotes.Business.Models;

// The OAuth rules of a Git connection: the random state and PKCE verifier (RFC 7636, S256), their comparison,
// the account values that fit dbo.GitConnections and when a token is refreshed.
public static class GitAuthorizationRules
{
    public const int AccountIdMaxLength = 50;
    public const int AccountLoginMaxLength = 100;
    public const int ScopesMaxLength = 500;

    // A token that expires within this margin is refreshed first, so it does not expire during the request.
    public static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);

    // 32 random bytes: a 43-character value, the PKCE verifier's minimum length and more than enough for the state.
    public static string NewState() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string NewCodeVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string CodeChallenge(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);
        return Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
    }

    // Compared in constant time; a missing value never matches.
    public static bool StateMatches(string? expected, string? received) =>
        !string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(received)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(received));

    public static bool ValidAccount(GitAccount account) =>
        Valid(account.Id, AccountIdMaxLength) && Valid(account.Login, AccountLoginMaxLength);

    // The scopes as stored: informative only, so null when empty or longer than the column.
    public static string? NormalizeScopes(string? scopes) =>
        scopes?.Trim() is { Length: > 0 and <= ScopesMaxLength } trimmed ? trimmed : null;

    public static bool NeedsRefresh(GitTokens tokens, DateTime nowUtc) =>
        tokens.AccessTokenExpiresAtUtc is { } expires && expires <= nowUtc + RefreshMargin;

    public static bool CanRefresh(GitTokens tokens, DateTime nowUtc) =>
        !string.IsNullOrEmpty(tokens.RefreshToken)
        && (tokens.RefreshTokenExpiresAtUtc is not { } expires || expires > nowUtc);

    private static bool Valid(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength && !value.Any(char.IsControl);

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
