using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;

namespace IsSuas.Domain.Documents;

/// <summary>
/// ENT-080/081 — logický dokument + verzia v jednej tabuľke (family = dokument, riadok = verzia).
/// ENT-082 — blob cez <see cref="StoredFileId"/>.
/// </summary>
public class ManagedDocument : AuditableEntity
{
    /// <summary>Groups all versions of the same logical document in a scope (ENT-080).</summary>
    public Guid DocumentFamilyId { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string DocumentTypeCode { get; set; } = "GENERAL";
    public LinkedEntityType LinkedEntityType { get; set; }
    public Guid LinkedEntityId { get; set; }
    public int Version { get; set; } = 1;
    public bool IsCurrent { get; set; } = true;
    public Guid? SupersedesDocumentId { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Current;
    public DocumentSensitivity Sensitivity { get; set; } = DocumentSensitivity.Internal;
    public Guid OwnerAccountId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public Guid? StoredFileId { get; set; }
    public StoredFile? StoredFile { get; set; }
    public Guid? RetentionRuleId { get; set; }
    public RetentionRule? RetentionRule { get; set; }
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public bool IsMandatory { get; set; }
    public bool BlocksWhenInvalid { get; set; }
    public string? ScanStatus { get; set; } = "SkippedDev";
    public string? Notes { get; set; }
}

public class FormTemplate : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameSk { get; set; } = string.Empty;
    public FormTemplateKind Kind { get; set; } = FormTemplateKind.Checklist;
    public int Version { get; set; } = 1;
    public bool IsCurrent { get; set; } = true;
    public Guid? SupersedesTemplateId { get; set; }
    public string? DescriptionSk { get; set; }
    public ICollection<FormTemplateItem> Items { get; set; } = new List<FormTemplateItem>();
}

public class FormTemplateItem : AuditableEntity
{
    public Guid FormTemplateId { get; set; }
    public FormTemplate FormTemplate { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string LabelSk { get; set; } = string.Empty;
    public FormItemType ItemType { get; set; } = FormItemType.YesNo;
    public bool IsRequired { get; set; } = true;
    public bool IsBlocking { get; set; }
    public int SortOrder { get; set; }
    public string? OptionsJson { get; set; }
    public string? VisibleWhenJson { get; set; }
    public string? ValidationJson { get; set; }
}

public class FormSubmission : AuditableEntity
{
    public Guid FormTemplateId { get; set; }
    public FormTemplate FormTemplate { get; set; } = null!;
    /// <summary>Frozen template version at fill time.</summary>
    public int TemplateVersion { get; set; }
    public LinkedEntityType LinkedEntityType { get; set; }
    public Guid LinkedEntityId { get; set; }
    public FormSubmissionStatus Status { get; set; } = FormSubmissionStatus.Draft;
    public Guid FilledByAccountId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool IsComplete { get; set; }
    public bool HasBlockingIncomplete { get; set; }
    public ICollection<FormAnswer> Answers { get; set; } = new List<FormAnswer>();
}

public class FormAnswer : AuditableEntity
{
    public Guid FormSubmissionId { get; set; }
    public FormSubmission FormSubmission { get; set; } = null!;
    public Guid FormTemplateItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? ValueText { get; set; }
    public bool? ValueBool { get; set; }
    public decimal? ValueNumber { get; set; }
    public DateTimeOffset? ValueDate { get; set; }
}

public class RiskAssessment : AuditableEntity
{
    public Guid? FormSubmissionId { get; set; }
    public LinkedEntityType LinkedEntityType { get; set; }
    public Guid LinkedEntityId { get; set; }
    public string MethodologyCode { get; set; } = "LIKELIHOOD_SEVERITY_V1";
    public int Likelihood { get; set; }
    public int Severity { get; set; }
    public int Score { get; set; }
    public RiskClassification Classification { get; set; }
    public string? Notes { get; set; }
    public Guid AssessedByAccountId { get; set; }
    public DateTimeOffset AssessedAt { get; set; }
}
