namespace WorkNotes.Business.Models;

// A connection whose access token can be used now: Credential (with its tokens) is set exactly when Status is Valid.
public sealed record GitAccessToken(GitVerifyStatus Status, GitCredential? Credential)
{
    public static GitAccessToken Failed(GitVerifyStatus status) => new(status, null);
}
