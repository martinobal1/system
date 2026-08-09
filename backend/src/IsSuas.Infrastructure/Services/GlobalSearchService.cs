using IsSuas.Application.Policies;
using IsSuas.Application.Search;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IGlobalSearchService
{
    Task<GlobalSearchResponse> SearchAsync(string? query, int limit = 8, CancellationToken cancellationToken = default);
}

public sealed class GlobalSearchService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy) : IGlobalSearchService
{
    public async Task<GlobalSearchResponse> SearchAsync(
        string? query,
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length < 2)
            return new GlobalSearchResponse(q, [], new Dictionary<string, int>());

        limit = Math.Clamp(limit, 1, 20);
        var hits = new List<SearchHitDto>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        if (policy.HasPermission(Permissions.UasRead) && policy.CanAccessBusinessData())
        {
            var rows = await db.UasAircraft.AsNoTracking()
                .Include(u => u.OwningOrgUnit)
                .Where(u => u.IsActive && (
                    u.SerialNumber.Contains(q) ||
                    u.Model.Contains(q) ||
                    (u.RegistrationNumber != null && u.RegistrationNumber.Contains(q)) ||
                    u.Manufacturer.Contains(q)))
                .OrderByDescending(u => u.UpdatedAt)
                .Take(limit * 3)
                .ToListAsync(cancellationToken);
            var filtered = rows
                .Where(u => u.OwningOrgUnit is null || policy.CanAccessOrgUnit(u.OwningOrgUnit.Path))
                .Take(limit)
                .Select(u => new SearchHitDto(
                    "Uas",
                    u.Id,
                    u.RegistrationNumber ?? u.SerialNumber,
                    $"{u.Manufacturer} {u.Model} ({u.SerialNumber})",
                    u.OwningOrgUnit?.Name,
                    u.Status.ToString()))
                .ToList();
            Add(hits, counts, "Uas", filtered);
        }

        if (policy.HasPermission(Permissions.PilotsRead) && policy.CanAccessBusinessData())
        {
            var rows = await db.Pilots.AsNoTracking()
                .Include(p => p.Person)
                .Include(p => p.OrgUnit)
                .Where(p => p.IsActive && (
                    (p.LicenseNumber != null && p.LicenseNumber.Contains(q)) ||
                    p.Person.FirstName.Contains(q) ||
                    p.Person.LastName.Contains(q) ||
                    ((p.Person.FirstName + " " + p.Person.LastName).Contains(q))))
                .OrderByDescending(p => p.UpdatedAt)
                .Take(limit * 3)
                .ToListAsync(cancellationToken);
            var filtered = rows
                .Where(p => policy.CanAccessOrgUnit(p.OrgUnit.Path))
                .Take(limit)
                .Select(p => new SearchHitDto(
                    "Pilot",
                    p.Id,
                    $"{p.Person.FirstName} {p.Person.LastName}".Trim(),
                    p.LicenseNumber,
                    p.OrgUnit.Name,
                    p.Status.ToString()))
                .ToList();
            Add(hits, counts, "Pilot", filtered);
        }

        if (policy.HasPermission(Permissions.OperationsRead) && policy.CanAccessBusinessData())
        {
            var rows = await db.Operations.AsNoTracking()
                .Include(o => o.OrgUnit)
                .Where(o => o.IsActive && (
                    o.Title.Contains(q) ||
                    (o.LocationAddress != null && o.LocationAddress.Contains(q)) ||
                    (o.Description != null && o.Description.Contains(q))))
                .OrderByDescending(o => o.UpdatedAt)
                .Take(limit * 3)
                .ToListAsync(cancellationToken);
            var filtered = rows
                .Where(o => policy.CanAccessOrgUnit(o.OrgUnit.Path))
                .Take(limit)
                .Select(o => new SearchHitDto(
                    "Operation",
                    o.Id,
                    o.Title,
                    o.LocationAddress,
                    o.OrgUnit.Name,
                    o.Status.ToString()))
                .ToList();
            Add(hits, counts, "Operation", filtered);
        }

        if (policy.HasPermission(Permissions.AccountsRead))
        {
            var rows = await db.Accounts.AsNoTracking()
                .Include(a => a.Person)
                .Where(a => a.IsActive && (
                    a.UserName.Contains(q) ||
                    a.Person.FirstName.Contains(q) ||
                    a.Person.LastName.Contains(q) ||
                    (a.Person.Email != null && a.Person.Email.Contains(q))))
                .OrderBy(a => a.UserName)
                .Take(limit)
                .ToListAsync(cancellationToken);
            var filtered = rows
                .Select(a => new SearchHitDto(
                    "Account",
                    a.Id,
                    a.UserName,
                    $"{a.Person.FirstName} {a.Person.LastName}".Trim(),
                    null,
                    a.Status.ToString()))
                .ToList();
            Add(hits, counts, "Account", filtered);
        }

        if (policy.HasPermission(Permissions.SafetyRead) && policy.CanAccessBusinessData())
        {
            var rows = await db.SafetyEvents.AsNoTracking()
                .Include(e => e.OrgUnit)
                .Where(e => e.IsActive && (
                    e.Title.Contains(q) ||
                    e.InitialReport.Contains(q) ||
                    (e.LocationDescription != null && e.LocationDescription.Contains(q))))
                .OrderByDescending(e => e.UpdatedAt)
                .Take(limit * 3)
                .ToListAsync(cancellationToken);
            var filtered = rows
                .Where(e => e.OrgUnit is null || policy.CanAccessOrgUnit(e.OrgUnit.Path))
                .Take(limit)
                .Select(e => new SearchHitDto(
                    "SafetyEvent",
                    e.Id,
                    e.Title,
                    e.LocationDescription,
                    e.OrgUnit?.Name,
                    e.Status.ToString()))
                .ToList();
            Add(hits, counts, "SafetyEvent", filtered);
        }

        if (policy.HasPermission(Permissions.ControlsRead) && policy.CanAccessBusinessData())
        {
            var rows = await db.ControlAudits.AsNoTracking()
                .Include(c => c.OrgUnit)
                .Where(c => c.IsActive && (
                    c.Title.Contains(q) ||
                    (c.ScopeDescription != null && c.ScopeDescription.Contains(q))))
                .OrderByDescending(c => c.UpdatedAt)
                .Take(limit * 3)
                .ToListAsync(cancellationToken);
            var filtered = rows
                .Where(c => policy.CanAccessOrgUnit(c.OrgUnit.Path))
                .Take(limit)
                .Select(c => new SearchHitDto(
                    "ControlAudit",
                    c.Id,
                    c.Title,
                    c.Type.ToString(),
                    c.OrgUnit.Name,
                    c.Status.ToString()))
                .ToList();
            Add(hits, counts, "ControlAudit", filtered);
        }

        return new GlobalSearchResponse(q, hits, counts);
    }

    private static void Add(
        List<SearchHitDto> hits,
        Dictionary<string, int> counts,
        string type,
        List<SearchHitDto> items)
    {
        if (items.Count == 0)
            return;
        hits.AddRange(items);
        counts[type] = items.Count;
    }
}
