namespace IsSuas.Domain.Enums;

/// <summary>Typ uzla v organizačnom strome ATLAS.</summary>
public enum OrgUnitKind
{
    /// <summary>MV SR / koreň.</summary>
    Root = 0,
    /// <summary>Policajný zbor.</summary>
    BranchPz = 1,
    /// <summary>Hasičský a záchranný zbor.</summary>
    BranchHazz = 2,
    /// <summary>Civilná ochrana.</summary>
    BranchCo = 3,
    /// <summary>Bežný útvar (kraj, okres, stanica…).</summary>
    Unit = 4
}
