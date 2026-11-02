using IsSuas.Application.Policies;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

/// <summary>
/// Pravidlá „nadriadený prevádzkovateľ pilota“: prevádzkovateľom (účet úrovne 2) môže byť len ten,
/// ktorého domovský útvar je útvar pilota alebo niektorý jeho nadradený útvar.
/// </summary>
internal static class PilotOperatorRules
{
    /// <summary>Prevádzkovatelia, ktorí môžu viesť pilota z daného útvaru (najbližší útvar prvý).</summary>
    public static async Task<List<Account>> CandidatesAsync(IsSuasDbContext db, string pilotOrgPath, CancellationToken ct)
    {
        var ops = await db.Accounts
            .Include(a => a.Person).ThenInclude(p => p.HomeOrgUnit)
            .Where(a => a.IsActive && a.Status == AccountStatus.Active && a.AccessLevel == AccessLevel.Level2
                && a.Person.HomeOrgUnit != null && a.Person.HomeOrgUnit.IsActive)
            .ToListAsync(ct);
        return ops
            .Where(a => pilotOrgPath.StartsWith(a.Person.HomeOrgUnit!.Path, StringComparison.Ordinal))
            .OrderByDescending(a => a.Person.HomeOrgUnit!.Path.Length)
            .ThenBy(a => DisplayName(a), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static string DisplayName(Account a)
    {
        var n = $"{a.Person?.FirstName} {a.Person?.LastName}".Trim();
        return n.Length > 0 ? n : a.UserName;
    }

    /// <summary>
    /// Určí nadriadeného pri vytváraní pilota: zvolený (musí spĺňať pravidlo), inak volajúci prevádzkovateľ,
    /// inak jediný možný prevádzkovateľ útvaru, inak nikto (doplní sa neskôr).
    /// </summary>
    public static async Task<Guid?> ResolveForNewPilotAsync(IsSuasDbContext db, Guid? requested, string pilotOrgPath, Guid? callerAccountId, CancellationToken ct)
    {
        var candidates = await CandidatesAsync(db, pilotOrgPath, ct);
        if (requested is Guid r)
        {
            if (!candidates.Any(c => c.Id == r))
                throw new ValidationException("Zvolený prevádzkovateľ nemôže viesť pilota z tohto útvaru.");
            return r;
        }
        if (callerAccountId is Guid me && candidates.Any(c => c.Id == me)) return me;
        return candidates.Count == 1 ? candidates[0].Id : null;
    }
}
