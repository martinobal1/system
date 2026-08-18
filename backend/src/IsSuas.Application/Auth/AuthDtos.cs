namespace IsSuas.Application.Auth;

public sealed record LoginRequest(string UserName, string Password);
public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid AccountId,
    string UserName,
    string DisplayName,
    int AccessLevel,
    string LicenseStatus,
    IReadOnlyList<string> RoleCodes,
    Guid PersonId);
public sealed record SsoStartResponse(string Status, string Message, string? RedirectUrl, string? ReturnUrl);
public sealed record SsoCallbackResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid AccountId,
    string UserName,
    string DisplayName,
    string? ReturnUrl,
    int AccessLevel = 1,
    string LicenseStatus = "NotApplicable",
    IReadOnlyList<string>? RoleCodes = null,
    Guid PersonId = default);
public sealed record ReauthRequest(string Password);
public sealed record ReauthResponse(string ReauthToken, DateTimeOffset ExpiresAt);
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword);
