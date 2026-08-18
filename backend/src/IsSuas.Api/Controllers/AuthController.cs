using IsSuas.Application.Auth;
using IsSuas.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService, IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        => Ok(await authService.LoginAsync(request, cancellationToken));

    /// <summary>
    /// Stub pre OIDC/SAML SSO s identitou MV SR. Do dodania IdP parametrov použite lokálny login.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("sso/start")]
    public ActionResult<SsoStartResponse> SsoStart([FromQuery] string? returnUrl = null)
    {
        var authority = configuration["Auth:Oidc:Authority"];
        if (string.IsNullOrWhiteSpace(authority))
        {
            var redirectUri = configuration["Auth:Oidc:RedirectUri"] ?? "http://localhost:5211/auth/callback";
            var demo =
                $"{redirectUri}?code={Uri.EscapeDataString("dev-sso:gestor")}" +
                (string.IsNullOrWhiteSpace(returnUrl) ? "" : $"&state={Uri.EscapeDataString(returnUrl)}");
            return Ok(new SsoStartResponse(
                "dev",
                "DEV SSO: bez Authority použite callback s code=dev-sso:{userName} (demo redirectUrl pre gestora).",
                demo,
                returnUrl));
        }

        var clientId = configuration["Auth:Oidc:ClientId"] ?? "is-suas";
        var oidcRedirect = configuration["Auth:Oidc:RedirectUri"] ?? "http://localhost:5211/auth/callback";
        var redirect =
            $"{authority.TrimEnd('/')}/authorize" +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&response_type=code" +
            $"&scope=openid%20profile" +
            $"&redirect_uri={Uri.EscapeDataString(oidcRedirect)}";
        if (!string.IsNullOrWhiteSpace(returnUrl))
            redirect += $"&state={Uri.EscapeDataString(returnUrl)}";
        return Ok(new SsoStartResponse("ready", "Presmerujte používateľa na redirectUrl (OIDC authorize).", redirect, returnUrl));
    }

    [AllowAnonymous]
    [HttpGet("sso/callback")]
    public async Task<ActionResult<SsoCallbackResponse>> SsoCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
        => Ok(await authService.CompleteSsoCallbackAsync(code, state, cancellationToken));

    [Authorize]
    [HttpPost("reauth")]
    public async Task<ActionResult<ReauthResponse>> Reauth([FromBody] ReauthRequest request, CancellationToken cancellationToken)
        => Ok(await authService.ReauthAsync(request, cancellationToken));

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await authService.ChangePasswordAsync(request, cancellationToken);
        return NoContent();
    }
}
