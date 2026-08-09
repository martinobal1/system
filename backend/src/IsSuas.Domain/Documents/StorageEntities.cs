using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;

namespace IsSuas.Domain.Documents;

/// <summary>ENT-082 — elektronický súbor v object storage (blob + hash).</summary>
public class StoredFile : AuditableEntity
{
    public string StorageKey { get; set; } = string.Empty;
    public string Provider { get; set; } = "Local";
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string? ScanStatus { get; set; } = "SkippedDev";
}

/// <summary>ENT-087 — retenčné pravidlo.</summary>
public class RetentionRule : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameSk { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public string ArchiveMode { get; set; } = "Keep";
    public string? DescriptionSk { get; set; }
}

/// <summary>ENT-084 — elektronický spis / priečinok.</summary>
public class DocumentDossier : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? DescriptionSk { get; set; }
    public LinkedEntityType? LinkedEntityType { get; set; }
    public Guid? LinkedEntityId { get; set; }
    public ICollection<DocumentDossierItem> Items { get; set; } = new List<DocumentDossierItem>();
}

public class DocumentDossierItem : AuditableEntity
{
    public Guid DocumentDossierId { get; set; }
    public DocumentDossier DocumentDossier { get; set; } = null!;
    /// <summary>Odkaz na verziu dokumentu (ManagedDocument).</summary>
    public Guid ManagedDocumentId { get; set; }
    public ManagedDocument ManagedDocument { get; set; } = null!;
}
