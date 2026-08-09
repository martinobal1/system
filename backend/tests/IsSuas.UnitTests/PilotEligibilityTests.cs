using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;
using IsSuas.Domain.Pilots;

namespace IsSuas.UnitTests;

public class PilotEligibilityTests
{
    [Fact]
    public void FitPilot_WithValidCertificate_IsEligible()
    {
        var pilot = CreatePilot(PilotStatus.Fit);
        pilot.Certificates.Add(new PilotCertificate
        {
            Status = CertificateStatus.Issued,
            ValidFrom = DateTimeOffset.UtcNow.AddMonths(-1),
            ValidTo = DateTimeOffset.UtcNow.AddMonths(6),
            IsActive = true
        });

        Assert.True(PilotEligibility.CanAssignToOperation(pilot, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void UnfitPilot_IsNotEligible()
    {
        var pilot = CreatePilot(PilotStatus.Unfit);
        pilot.Certificates.Add(new PilotCertificate
        {
            Status = CertificateStatus.Issued,
            ValidFrom = DateTimeOffset.UtcNow.AddMonths(-1),
            ValidTo = DateTimeOffset.UtcNow.AddMonths(6),
            IsActive = true
        });

        Assert.False(PilotEligibility.CanAssignToOperation(pilot, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void FitPilot_WithoutValidCertificate_IsNotEligible()
    {
        var pilot = CreatePilot(PilotStatus.Fit);
        Assert.False(PilotEligibility.CanAssignToOperation(pilot, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void FitPilot_WithBlockingRestriction_IsNotEligible()
    {
        var pilot = CreatePilot(PilotStatus.Fit);
        pilot.Certificates.Add(new PilotCertificate
        {
            Status = CertificateStatus.Issued,
            ValidFrom = DateTimeOffset.UtcNow.AddMonths(-1),
            ValidTo = DateTimeOffset.UtcNow.AddMonths(6),
            IsActive = true
        });
        pilot.Restrictions.Add(new PilotRestriction
        {
            BlocksOperation = true,
            Status = PilotRestrictionStatus.Active,
            ValidFrom = DateTimeOffset.UtcNow.AddDays(-1),
            IsActive = true,
            Description = "Zdravotné obmedzenie",
            RestrictionTypeCode = "MEDICAL",
            SeverityCode = "HIGH"
        });

        Assert.False(PilotEligibility.CanAssignToOperation(pilot, DateTimeOffset.UtcNow));
    }

    private static Pilot CreatePilot(PilotStatus status) => new()
    {
        Status = status,
        IsActive = true,
        Person = new Person { FirstName = "A", LastName = "B" },
        OrgUnit = new OrgUnit { Name = "X", Code = "X", Path = "/x/" }
    };
}
