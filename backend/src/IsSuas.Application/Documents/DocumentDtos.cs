using IsSuas.Domain.Enums;

namespace IsSuas.Application.Documents;

public sealed record ManagedDocumentDto(
    Guid Id,
    Guid DocumentFamilyId,
    string Title,
    string DocumentTypeCode,
    LinkedEntityType LinkedEntityType,
    Guid LinkedEntityId,
    int Version,
    bool IsCurrent,
    DocumentStatus Status,
    DocumentSensitivity Sensitivity,
    Guid OwnerAccountId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string ContentHash,
    Guid? StoredFileId,
    bool HasStoredContent,
    Guid? RetentionRuleId,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    bool IsMandatory,
    bool BlocksWhenInvalid,
    string? ScanStatus,
    bool IsActive,
    uint RowVersion);

public sealed record UploadDocumentRequest(
    string Title,
    string DocumentTypeCode,
    LinkedEntityType LinkedEntityType,
    Guid LinkedEntityId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string ContentHash,
    DocumentSensitivity Sensitivity,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    bool IsMandatory,
    bool BlocksWhenInvalid,
    Guid? SupersedesDocumentId,
    string? Notes,
    string? ContentBase64 = null,
    Guid? RetentionRuleId = null);

public sealed record RetentionRuleDto(Guid Id, string Code, string NameSk, int RetentionDays, string ArchiveMode, string? DescriptionSk);
public sealed record DocumentDossierDto(Guid Id, string Title, string? DescriptionSk, LinkedEntityType? LinkedEntityType, Guid? LinkedEntityId, IReadOnlyList<Guid> DocumentIds);
public sealed record CreateDocumentDossierRequest(string Title, string? DescriptionSk, LinkedEntityType? LinkedEntityType, Guid? LinkedEntityId);
public sealed record AddDossierDocumentRequest(Guid ManagedDocumentId);

public sealed record FormTemplateDto(
    Guid Id,
    string Code,
    string NameSk,
    FormTemplateKind Kind,
    int Version,
    bool IsCurrent,
    string? DescriptionSk,
    IReadOnlyList<FormTemplateItemDto> Items,
    uint RowVersion);

public sealed record FormTemplateItemDto(
    Guid Id,
    string Code,
    string LabelSk,
    FormItemType ItemType,
    bool IsRequired,
    bool IsBlocking,
    int SortOrder,
    string? OptionsJson);

public sealed record CreateFormTemplateRequest(
    string Code,
    string NameSk,
    FormTemplateKind Kind,
    string? DescriptionSk,
    IReadOnlyList<CreateFormTemplateItemRequest> Items);

public sealed record CreateFormTemplateItemRequest(
    string Code,
    string LabelSk,
    FormItemType ItemType,
    bool IsRequired,
    bool IsBlocking,
    int SortOrder,
    string? OptionsJson);

public sealed record FormSubmissionDto(
    Guid Id,
    Guid FormTemplateId,
    string TemplateCode,
    int TemplateVersion,
    LinkedEntityType LinkedEntityType,
    Guid LinkedEntityId,
    FormSubmissionStatus Status,
    bool IsComplete,
    bool HasBlockingIncomplete,
    IReadOnlyList<FormAnswerDto> Answers,
    uint RowVersion);

public sealed record FormAnswerDto(
    Guid FormTemplateItemId,
    string ItemCode,
    string? ValueText,
    bool? ValueBool,
    decimal? ValueNumber,
    DateTimeOffset? ValueDate);

public sealed record StartFormSubmissionRequest(
    Guid FormTemplateId,
    LinkedEntityType LinkedEntityType,
    Guid LinkedEntityId);

public sealed record SaveFormAnswersRequest(
    IReadOnlyList<FormAnswerDto> Answers,
    bool MarkCompleted,
    uint RowVersion);

public sealed record RiskAssessmentDto(
    Guid Id,
    LinkedEntityType LinkedEntityType,
    Guid LinkedEntityId,
    string MethodologyCode,
    int Likelihood,
    int Severity,
    int Score,
    RiskClassification Classification,
    string? Notes,
    Guid AssessedByAccountId,
    DateTimeOffset AssessedAt);

public sealed record CreateRiskAssessmentRequest(
    LinkedEntityType LinkedEntityType,
    Guid LinkedEntityId,
    int Likelihood,
    int Severity,
    string? Notes,
    Guid? FormSubmissionId);

public sealed record DocumentBlockCheckDto(bool IsBlocked, string Reason, IReadOnlyList<string> Details);

public static class RiskMethodology
{
    public static (int Score, RiskClassification Classification) Evaluate(int likelihood, int severity)
    {
        if (likelihood is < 1 or > 5)
            throw new Policies.ValidationException("Pravdepodobnosť musí byť 1–5.");
        if (severity is < 1 or > 5)
            throw new Policies.ValidationException("Závažnosť musí byť 1–5.");

        var score = likelihood * severity;
        var classification = score switch
        {
            <= 4 => RiskClassification.Low,
            <= 9 => RiskClassification.Medium,
            <= 15 => RiskClassification.High,
            _ => RiskClassification.Critical
        };
        return (score, classification);
    }
}
