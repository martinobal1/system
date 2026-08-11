namespace IsSuas.Domain.Enums;

/// <summary>ATLAS prístupová úroveň (daco2): 1=gestor, 2=prevádzkovateľ, 3=pilot.</summary>
public enum AccessLevel
{
    Level1 = 1,
    Level2 = 2,
    Level3 = 3
}

public enum LicenseStatus
{
    NotApplicable = 0,
    Pending = 1,
    Confirmed = 2,
    Rejected = 3
}
