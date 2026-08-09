namespace IsSuas.Application.Auth;

public sealed record LoginRequest(string UserName, string Password);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, Guid AccountId, string UserName, string DisplayName);
public sealed record SsoStartResponse(string Status, string Message, string? RedirectUrl, string? ReturnUrl);
public sealed record SsoCallbackResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid AccountId,
    string UserName,
    string DisplayName,
    string? ReturnUrl);
public sealed record ReauthRequest(string Password);
public sealed record ReauthResponse(string ReauthToken, DateTimeOffset ExpiresAt);
