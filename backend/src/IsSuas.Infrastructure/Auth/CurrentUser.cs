using System.Security.Claims;
using IsSuas.Application.Abstractions;
using IsSuas.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace IsSuas.Infrastructure.Auth;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? AccountId
    {
        get
        {
            var value = Principal?.FindFirstValue("account_id") ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserName => Principal?.FindFirstValue(ClaimTypes.Name) ?? Principal?.Identity?.Name;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool IsSystemAdministrator =>
        Roles.Any(r => r.RoleCode == nameof(SystemRole.SystemAdministrator));

    public IReadOnlyList<EffectiveRole> Roles
    {
        get
        {
            if (Principal is null)
                return [];

            var roles = new List<EffectiveRole>();
            var roleClaims = Principal.FindAll("role_assignment");
            foreach (var claim in roleClaims)
            {
                // format: roleId|roleCode|orgUnitId|orgUnitPath|grantsBusiness
                var parts = claim.Value.Split('|');
                if (parts.Length < 5)
                    continue;
                if (!Guid.TryParse(parts[0], out var roleId))
                    continue;
                if (!Guid.TryParse(parts[2], out var orgUnitId))
                    continue;
                roles.Add(new EffectiveRole(
                    roleId,
                    parts[1],
                    orgUnitId,
                    parts[3],
                    parts[4] == "1"));
            }

            return roles;
        }
    }
}
