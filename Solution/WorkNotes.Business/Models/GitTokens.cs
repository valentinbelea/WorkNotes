namespace WorkNotes.Business.Models;

// The OAuth tokens of a connection. They circulate only between GitHubConnectionService, the provider client and the
// repository, which stores them encrypted; they never reach a page, TempData, a message or a log.
// The refresh token and the expiry dates are null when the provider issues tokens that do not expire.
public sealed record GitTokens(
    string AccessToken,
    string? RefreshToken,
    DateTime? AccessTokenExpiresAtUtc,
    DateTime? RefreshTokenExpiresAtUtc,
    string? Scopes)
{
    // A record prints its members: this one leaves the tokens out, so it can be logged or inspected safely.
    public override string ToString() =>
        $"GitTokens {{ AccessTokenExpiresAtUtc = {AccessTokenExpiresAtUtc:O}, HasRefreshToken = {RefreshToken is not null}, Scopes = {Scopes} }}";
}
