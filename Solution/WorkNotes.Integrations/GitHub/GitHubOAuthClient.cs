using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using WorkNotes.Business.Abstractions;
using WorkNotes.Business.Models;

namespace WorkNotes.Integrations.GitHub;

// GitHub's OAuth web flow (https://docs.github.com/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps) and the
// user and authorization endpoints of its REST API. Tokens travel only in request bodies and the Authorization header,
// never in an address, so the HttpClient logs never contain them.
public sealed class GitHubOAuthClient(HttpClient http, IOptions<GitHubOptions> options, TimeProvider time) : IGitHubOAuthClient
{
    private const string ApiVersion = "2022-11-28";
    private readonly GitHubOptions settings = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(settings.ClientId) && !string.IsNullOrWhiteSpace(settings.ClientSecret);

    public string GetAuthorizationUrl(string state, string codeChallenge, string redirectUri)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = settings.ClientId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["allow_signup"] = "false"
        };
        if (!string.IsNullOrWhiteSpace(settings.Scopes)) query["scope"] = settings.Scopes.Trim();
        return QueryHelpers.AddQueryString(settings.AuthorizationEndpoint.AbsoluteUri, query);
    }

    public Task<GitProviderResult<GitTokens>> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri,
        CancellationToken cancellationToken) =>
        RequestTokensAsync(new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId!,
            ["client_secret"] = settings.ClientSecret!,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        }, cancellationToken);

    public Task<GitProviderResult<GitTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        RequestTokensAsync(new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId!,
            ["client_secret"] = settings.ClientSecret!,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        }, cancellationToken);

    public async Task<GitProviderResult<GitAccount>> GetAccountAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = ApiRequest(HttpMethod.Get, "user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await SendAsync(request, cancellationToken);
        if (response is null || Unavailable(response.StatusCode)) return GitProviderResult<GitAccount>.Unavailable;
        if (!response.IsSuccessStatusCode) return GitProviderResult<GitAccount>.Rejected;

        var (read, user) = await ReadAsync<UserResponse>(response, cancellationToken);
        if (!read) return GitProviderResult<GitAccount>.Unavailable;
        return user is { Id: > 0, Login: { Length: > 0 } login }
            ? GitProviderResult<GitAccount>.Succeeded(new GitAccount(user.Id.ToString(CultureInfo.InvariantCulture), login))
            : GitProviderResult<GitAccount>.Rejected;
    }

    public async Task<GitProviderStatus> RevokeAsync(string accessToken, CancellationToken cancellationToken)
    {
        // DELETE /applications/{client_id}/grant, authenticated as the application: removes the user's authorization.
        using var request = ApiRequest(HttpMethod.Delete, $"applications/{Uri.EscapeDataString(settings.ClientId!)}/grant");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.ClientSecret}")));
        request.Content = JsonContent.Create(new RevokeRequest(accessToken));
        using var response = await SendAsync(request, cancellationToken);
        if (response is null || Unavailable(response.StatusCode)) return GitProviderStatus.Unavailable;
        return response.IsSuccessStatusCode ? GitProviderStatus.Succeeded : GitProviderStatus.Rejected;
    }

    // The token endpoint answers 200 with an "error" member when it refuses a code or a refresh token.
    private async Task<GitProviderResult<GitTokens>> RequestTokensAsync(Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await SendAsync(request, cancellationToken);
        if (response is null || Unavailable(response.StatusCode)) return GitProviderResult<GitTokens>.Unavailable;
        if (!response.IsSuccessStatusCode) return GitProviderResult<GitTokens>.Rejected;

        var (read, body) = await ReadAsync<TokenResponse>(response, cancellationToken);
        if (!read) return GitProviderResult<GitTokens>.Unavailable;
        if (body is null || body.Error is not null || string.IsNullOrEmpty(body.AccessToken))
            return GitProviderResult<GitTokens>.Rejected;

        var now = time.GetUtcNow().UtcDateTime;
        return GitProviderResult<GitTokens>.Succeeded(new GitTokens(
            body.AccessToken,
            string.IsNullOrEmpty(body.RefreshToken) ? null : body.RefreshToken,
            ExpiresAt(now, body.ExpiresIn),
            ExpiresAt(now, body.RefreshTokenExpiresIn),
            body.Scope));
    }

    private HttpRequestMessage ApiRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(settings.ApiBaseAddress, path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", ApiVersion);
        return request;
    }

    // Null when GitHub cannot be reached or the request timed out; the caller's cancellation is propagated.
    private async Task<HttpResponseMessage?> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    // False when the body is not the JSON GitHub sends (a proxy page, for example): GitHub did not really answer.
    private static async Task<(bool Read, T? Value)> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return (true, await response.Content.ReadFromJsonAsync<T>(cancellationToken));
        }
        catch (JsonException)
        {
            return (false, default);
        }
        catch (NotSupportedException)
        {
            return (false, default);
        }
    }

    // 403 and 429 are GitHub's rate limits: the request may succeed later, so they are not a refusal.
    private static bool Unavailable(HttpStatusCode status) =>
        (int)status >= 500 || status is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests;

    private static DateTime? ExpiresAt(DateTime nowUtc, long? seconds)
    {
        if (seconds is not > 0) return null;
        var expires = nowUtc.AddSeconds(seconds.Value);
        return expires.AddTicks(-(expires.Ticks % TimeSpan.TicksPerSecond));
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] long? ExpiresIn,
        [property: JsonPropertyName("refresh_token_expires_in")] long? RefreshTokenExpiresIn,
        [property: JsonPropertyName("scope")] string? Scope,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record UserResponse(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("login")] string? Login);

    private sealed record RevokeRequest([property: JsonPropertyName("access_token")] string AccessToken);
}
