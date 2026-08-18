using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IsSuas.Application.Abstractions;
using IsSuas.Application.Auth;
using IsSuas.Application.Policies;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace IsSuas.Infrastructure.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<SsoCallbackResponse> CompleteSsoCallbackAsync(string? code, string? state, CancellationToken cancellationToken = default);
    Task<ReauthResponse> ReauthAsync(ReauthRequest request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
}

public sealed class AuthService(
    IsSuasDbContext db,
    IConfiguration configuration,
    ICurrentUser currentUser,
    IAuditWriter audit) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var userName = (request.UserName ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            throw new UnauthorizedAccessException("Neplatné prihlasovacie údaje.");

        var account = await LoadAccountByUserNameAsync(userName, cancellationToken);
        if (account is null || !BCrypt.Net.BCrypt.Verify(password, account.PasswordHash))
            throw new UnauthorizedAccessException("Neplatné prihlasovacie údaje.");

        EnsureAccountActive(account);
        return await IssueLoginAsync(account, writeSession: true, cancellationToken);
    }

    public async Task<SsoCallbackResponse> CompleteSsoCallbackAsync(
        string? code,
        string? state,
        CancellationToken cancellationToken = default)
    {
        var authority = configuration["Auth:Oidc:Authority"];
        if (!string.IsNullOrWhiteSpace(authority))
            throw new NotImplementedException(
                "OIDC token exchange ešte nie je napojený. Nastavte Auth:Oidc:Authority až po implementácii IdP výmeny kódu.");

        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException("Chýba authorization code.");

        const string prefix = "dev-sso:";
        if (!code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("DEV SSO vyžaduje kód vo formáte dev-sso:{userName}.");

        var userName = code[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(userName))
            throw new ValidationException("V kóde chýba používateľské meno.");

        var account = await LoadAccountByUserNameAsync(userName, cancellationToken)
            ?? throw new UnauthorizedAccessException("Účet pre SSO kód nebol nájdený.");
        EnsureAccountActive(account);

        var login = await IssueLoginAsync(account, writeSession: true, cancellationToken);
        return new SsoCallbackResponse(
            login.AccessToken,
            login.ExpiresAt,
            login.AccountId,
            login.UserName,
            login.DisplayName,
            string.IsNullOrWhiteSpace(state) ? null : state,
            login.AccessLevel,
            login.LicenseStatus,
            login.RoleCodes,
            login.PersonId);
    }

    public async Task<ReauthResponse> ReauthAsync(ReauthRequest request, CancellationToken cancellationToken = default)
    {
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("Heslo je povinné.");

        var account = await db.Accounts
            .Include(a => a.Person)
            .FirstOrDefaultAsync(a => a.Id == currentUser.AccountId && a.IsActive, cancellationToken)
            ?? throw new UnauthorizedAccessException("Účet nebol nájdený.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
            throw new UnauthorizedAccessException("Neplatné heslo pre opätovné overenie.");

        EnsureAccountActive(account);

        var now = DateTimeOffset.UtcNow;
        var minutes = int.TryParse(configuration["Auth:Reauth:ExpiryMinutes"], out var m) ? m : 5;
        minutes = Math.Clamp(minutes, 1, 30);
        var expires = now.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new("account_id", account.Id.ToString()),
            new(ClaimTypes.Name, account.UserName),
            new("reauth", "1")
        };

        var token = CreateJwt(claims, now, expires);
        return new ReauthResponse(token, expires);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");

        var currentPassword = request.CurrentPassword ?? string.Empty;
        var newPassword = (request.NewPassword ?? string.Empty).Trim();
        var confirmPassword = (request.ConfirmNewPassword ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(currentPassword))
            throw new ValidationException("Aktuálne heslo je povinné.");
        if (string.IsNullOrWhiteSpace(newPassword))
            throw new ValidationException("Nové heslo je povinné.");
        if (newPassword.Length < 8)
            throw new ValidationException("Nové heslo musí mať aspoň 8 znakov.");
        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            throw new ValidationException("Nové heslo a potvrdenie sa nezhodujú.");

        var account = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == currentUser.AccountId && a.IsActive, cancellationToken)
            ?? throw new UnauthorizedAccessException("Účet nebol nájdený.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, account.PasswordHash))
            throw new ValidationException("Aktuálne heslo nie je správne.");
        if (BCrypt.Net.BCrypt.Verify(newPassword, account.PasswordHash))
            throw new ValidationException("Nové heslo musí byť iné ako aktuálne.");

        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        account.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(
            "Account.ChangePassword",
            nameof(Account),
            account.Id,
            null,
            new { account.UserName },
            cancellationToken: cancellationToken);
    }

    private async Task<Account?> LoadAccountByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        var normalized = userName.Trim().ToLowerInvariant();
        return await db.Accounts
            .Include(a => a.Person)
            .Include(a => a.RoleAssignments).ThenInclude(r => r.Role)
            .Include(a => a.RoleAssignments).ThenInclude(r => r.OrgUnit)
            .FirstOrDefaultAsync(a => a.UserName.ToLower() == normalized && a.IsActive, cancellationToken);
    }

    private static void EnsureAccountActive(Account account)
    {
        if (account.Status is AccountStatus.Deactivated or AccountStatus.TemporarilyBlocked or AccountStatus.Prepared)
            throw new UnauthorizedAccessException("Účet nie je aktívny.");
    }

    private async Task<LoginResponse> IssueLoginAsync(Account account, bool writeSession, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var activeAssignments = account.RoleAssignments
            .Where(r => r.IsActive && r.ValidFrom <= now && (r.ValidTo is null || r.ValidTo > now))
            .ToList();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new("account_id", account.Id.ToString()),
            new(ClaimTypes.Name, account.UserName)
        };

        foreach (var assignment in activeAssignments)
        {
            var grants = assignment.Role.GrantsBusinessDataAccess ? "1" : "0";
            claims.Add(new Claim(
                "role_assignment",
                $"{assignment.RoleId}|{assignment.Role.Code}|{assignment.OrgUnitId}|{assignment.OrgUnit.Path}|{grants}"));
            claims.Add(new Claim(ClaimTypes.Role, assignment.Role.Code.ToString()));
        }

        var expiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var m) ? m : 480;
        var expires = now.AddMinutes(expiryMinutes);
        var token = CreateJwt(claims, now, expires);

        if (writeSession)
        {
            account.LastLoginAt = now;
            db.UserSessions.Add(new UserSession
            {
                AccountId = account.Id,
                IssuedAt = now,
                ExpiresAt = expires,
                Result = "Success",
                CreatedBy = account.Id,
                UpdatedBy = account.Id
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return new LoginResponse(
            token,
            expires,
            account.Id,
            account.UserName,
            $"{account.Person.FirstName} {account.Person.LastName}",
            (int)account.AccessLevel,
            account.LicenseStatus.ToString(),
            activeAssignments.Select(a => a.Role.Code.ToString()).Distinct().ToList(),
            account.PersonId);
    }

    private string CreateJwt(IEnumerable<Claim> claims, DateTimeOffset now, DateTimeOffset expires)
    {
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "IsSuas",
            audience: configuration["Jwt:Audience"] ?? "IsSuas",
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
