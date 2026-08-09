using IsSuas.Application.Abstractions;
using IsSuas.Application.Documents;
using IsSuas.Application.Notifications;
using IsSuas.Application.Policies;
using IsSuas.Domain.Documents;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Safety;
using IsSuas.Infrastructure.Persistence;
using IsSuas.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace IsSuas.Infrastructure.Services;

public interface IDocumentService
{
    Task<IReadOnlyList<ManagedDocumentDto>> ListDocumentsAsync(LinkedEntityType? entityType, Guid? entityId, CancellationToken cancellationToken = default);
    Task<ManagedDocumentDto> UploadAsync(UploadDocumentRequest request, CancellationToken cancellationToken = default);
    Task<(Stream Content, string FileName, string ContentType)> DownloadContentAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetentionRuleDto>> ListRetentionRulesAsync(CancellationToken cancellationToken = default);
    Task<DocumentDossierDto> CreateDossierAsync(CreateDocumentDossierRequest request, CancellationToken cancellationToken = default);
    Task<DocumentDossierDto> AddToDossierAsync(Guid dossierId, AddDossierDocumentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentDossierDto>> ListDossiersAsync(CancellationToken cancellationToken = default);
    Task<int> RefreshExpirationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default);
    Task<FormTemplateDto> CreateTemplateAsync(CreateFormTemplateRequest request, CancellationToken cancellationToken = default);
    Task<FormSubmissionDto> StartSubmissionAsync(StartFormSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<FormSubmissionDto> SaveAnswersAsync(Guid submissionId, SaveFormAnswersRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormSubmissionDto>> ListSubmissionsAsync(LinkedEntityType? entityType, Guid? entityId, CancellationToken cancellationToken = default);
    Task<RiskAssessmentDto> CreateRiskAssessmentAsync(CreateRiskAssessmentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RiskAssessmentDto>> ListRiskAssessmentsAsync(LinkedEntityType? entityType, Guid? entityId, CancellationToken cancellationToken = default);
    Task<DocumentBlockCheckDto> GetOperationDocumentBlockAsync(Guid operationId, CancellationToken cancellationToken = default);
}

public sealed class DocumentService(
    IsSuasDbContext db,
    ICurrentUser currentUser,
    IAuthorizationPolicyService policy,
    IAuditWriter audit,
    IClock clock,
    IObjectStorage objectStorage,
    INotificationPublisher notifications) : IDocumentService
{
    private static readonly HashSet<string> AllowedContentTypes =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain",
        "application/octet-stream"
    ];

    private const long MaxSizeBytes = 25 * 1024 * 1024;

    public async Task<IReadOnlyList<ManagedDocumentDto>> ListDocumentsAsync(
        LinkedEntityType? entityType, Guid? entityId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsRead);
        var query = db.ManagedDocuments.AsNoTracking().Where(d => d.IsActive);
        if (entityType is not null) query = query.Where(d => d.LinkedEntityType == entityType);
        if (entityId is not null) query = query.Where(d => d.LinkedEntityId == entityId);

        var items = await query.OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
        var filtered = new List<ManagedDocument>();
        foreach (var d in items)
        {
            if (d.Sensitivity >= DocumentSensitivity.Sensitive && !policy.HasPermission(Permissions.DocumentsSensitiveRead))
                continue;
            if (!await CanAccessLinkedEntityAsync(d.LinkedEntityType, d.LinkedEntityId, cancellationToken))
                continue;
            filtered.Add(d);
        }

        return filtered.Select(MapDocument).ToList();
    }

    public async Task<ManagedDocumentDto> UploadAsync(UploadDocumentRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.FileName))
            throw new ValidationException("Názov a súbor sú povinné.");

        byte[]? payload = null;
        string contentHash;
        long sizeBytes;
        if (!string.IsNullOrWhiteSpace(request.ContentBase64))
        {
            try { payload = Convert.FromBase64String(request.ContentBase64); }
            catch (FormatException) { throw new ValidationException("ContentBase64 nie je platný Base64."); }
            if (payload.Length == 0 || payload.LongLength > MaxSizeBytes)
                throw new ValidationException($"Veľkosť súboru musí byť 1 B – {MaxSizeBytes} B.");
            contentHash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(request.ContentHash)
                && !string.Equals(request.ContentHash.Trim(), contentHash, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Kontrolný súčet nezodpovedá obsahu súboru.");
            sizeBytes = payload.LongLength;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.ContentHash))
                throw new ValidationException("Kontrolný súčet je povinný (alebo pošlite ContentBase64).");
            if (request.SizeBytes <= 0 || request.SizeBytes > MaxSizeBytes)
                throw new ValidationException($"Veľkosť súboru musí byť 1 B – {MaxSizeBytes} B.");
            contentHash = request.ContentHash.Trim().ToLowerInvariant();
            sizeBytes = request.SizeBytes;
        }

        var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType.Trim();
        if (!AllowedContentTypes.Contains(contentType))
            throw new ValidationException($"Nepovolený typ súboru: {contentType}");
        if (request.Sensitivity >= DocumentSensitivity.Sensitive)
            policy.EnsurePermission(Permissions.DocumentsSensitiveRead);
        if (!await CanAccessLinkedEntityAsync(request.LinkedEntityType, request.LinkedEntityId, cancellationToken))
            throw new ForbiddenException("Prístup k viazanému záznamu bol zamietnutý.");

        if (request.RetentionRuleId is Guid ruleId
            && !await db.RetentionRules.AnyAsync(r => r.Id == ruleId && r.IsActive, cancellationToken))
            throw new NotFoundException("Retenčné pravidlo nebolo nájdené.");

        var familyId = Guid.NewGuid();
        var version = 1;
        ManagedDocument? previous = null;
        if (request.SupersedesDocumentId is Guid supersedesId)
        {
            previous = await db.ManagedDocuments.FirstOrDefaultAsync(d => d.Id == supersedesId, cancellationToken)
                ?? throw new NotFoundException("Nahrádzaný dokument nebol nájdený.");
            if (!previous.IsCurrent)
                throw new ValidationException("Nahradiť je možné iba aktuálnu verziu.");
            previous.IsCurrent = false;
            previous.Status = DocumentStatus.Superseded;
            previous.UpdatedBy = currentUser.AccountId;
            familyId = previous.DocumentFamilyId;
            version = previous.Version + 1;
        }

        Guid? storedFileId = null;
        if (payload is not null)
        {
            await using var stream = new MemoryStream(payload);
            var stored = await objectStorage.PutAsync(request.FileName.Trim(), contentType, stream, cancellationToken);
            var file = new StoredFile
            {
                StorageKey = stored.StorageKey,
                Provider = stored.Provider,
                FileName = request.FileName.Trim(),
                ContentType = contentType,
                SizeBytes = stored.SizeBytes,
                ContentHash = stored.ContentHash,
                ScanStatus = "SkippedDev",
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            };
            db.StoredFiles.Add(file);
            await db.SaveChangesAsync(cancellationToken);
            storedFileId = file.Id;
            contentHash = stored.ContentHash;
            sizeBytes = stored.SizeBytes;
        }

        var now = clock.UtcNow;
        var status = DocumentStatus.Current;
        if (request.ValidTo is not null && request.ValidTo < now)
            status = DocumentStatus.Expired;

        var doc = new ManagedDocument
        {
            DocumentFamilyId = familyId,
            Title = request.Title.Trim(),
            DocumentTypeCode = string.IsNullOrWhiteSpace(request.DocumentTypeCode) ? "GENERAL" : request.DocumentTypeCode.Trim(),
            LinkedEntityType = request.LinkedEntityType,
            LinkedEntityId = request.LinkedEntityId,
            Version = version,
            IsCurrent = status == DocumentStatus.Current,
            SupersedesDocumentId = request.SupersedesDocumentId,
            Status = status,
            Sensitivity = request.Sensitivity,
            OwnerAccountId = currentUser.AccountId.Value,
            FileName = request.FileName.Trim(),
            ContentType = contentType,
            SizeBytes = sizeBytes,
            ContentHash = contentHash,
            StoredFileId = storedFileId,
            RetentionRuleId = request.RetentionRuleId,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            IsMandatory = request.IsMandatory,
            BlocksWhenInvalid = request.BlocksWhenInvalid || request.IsMandatory,
            ScanStatus = "SkippedDev",
            Notes = request.Notes?.Trim(),
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.ManagedDocuments.Add(doc);
        await db.SaveChangesAsync(cancellationToken);
        if (doc.Status == DocumentStatus.Expired)
        {
            await notifications.PublishAsync(new PublishNotificationRequest(
                null,
                "Exspirácia dokumentu",
                $"Dokument „{doc.Title}“ exspiroval (verzia {doc.Version}).",
                "DOCUMENT_EXPIRED",
                "High",
                null,
                nameof(SystemRole.UnitAdministrator),
                null,
                nameof(ManagedDocument),
                doc.Id), cancellationToken);
        }
        await audit.WriteAsync("Document.Upload", nameof(ManagedDocument), doc.Id, previous is null ? null : MapDocument(previous), MapDocument(doc), cancellationToken: cancellationToken);
        return MapDocument(doc);
    }

    public async Task<(Stream Content, string FileName, string ContentType)> DownloadContentAsync(
        Guid documentId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsRead);
        var doc = await db.ManagedDocuments.AsNoTracking()
            .Include(d => d.StoredFile)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.IsActive, cancellationToken)
            ?? throw new NotFoundException("Dokument nebol nájdený.");
        if (doc.Sensitivity >= DocumentSensitivity.Sensitive)
            policy.EnsurePermission(Permissions.DocumentsSensitiveRead);
        if (!await CanAccessLinkedEntityAsync(doc.LinkedEntityType, doc.LinkedEntityId, cancellationToken))
            throw new ForbiddenException("Prístup k viazanému záznamu bol zamietnutý.");
        if (doc.StoredFile is null)
            throw new ValidationException("Dokument nemá uložený binárny obsah (iba metaúdaje).");

        var stream = await objectStorage.GetAsync(doc.StoredFile.StorageKey, cancellationToken);
        return (stream, doc.FileName, doc.ContentType);
    }

    public async Task<IReadOnlyList<RetentionRuleDto>> ListRetentionRulesAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsRead);
        return await db.RetentionRules.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Code)
            .Select(r => new RetentionRuleDto(r.Id, r.Code, r.NameSk, r.RetentionDays, r.ArchiveMode, r.DescriptionSk))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentDossierDto>> ListDossiersAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsRead);
        var items = await db.DocumentDossiers.AsNoTracking()
            .Include(d => d.Items)
            .Where(d => d.IsActive)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
        return items.Select(MapDossier).ToList();
    }

    public async Task<DocumentDossierDto> CreateDossierAsync(CreateDocumentDossierRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsWrite);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException("Názov spisu je povinný.");
        var dossier = new DocumentDossier
        {
            Title = request.Title.Trim(),
            DescriptionSk = request.DescriptionSk?.Trim(),
            LinkedEntityType = request.LinkedEntityType,
            LinkedEntityId = request.LinkedEntityId,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.DocumentDossiers.Add(dossier);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("DocumentDossier.Create", nameof(DocumentDossier), dossier.Id, null, MapDossier(dossier), cancellationToken: cancellationToken);
        return MapDossier(dossier);
    }

    public async Task<DocumentDossierDto> AddToDossierAsync(Guid dossierId, AddDossierDocumentRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsWrite);
        var dossier = await db.DocumentDossiers.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dossierId && d.IsActive, cancellationToken)
            ?? throw new NotFoundException("Spis nebol nájdený.");
        var doc = await db.ManagedDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.ManagedDocumentId && d.IsActive, cancellationToken)
            ?? throw new NotFoundException("Dokument nebol nájdený.");
        if (await db.DocumentDossierItems.AnyAsync(i => i.DocumentDossierId == dossierId && i.ManagedDocumentId == doc.Id && i.IsActive, cancellationToken))
            throw new ConflictException("Dokument je už v spise.");

        db.DocumentDossierItems.Add(new DocumentDossierItem
        {
            DocumentDossierId = dossierId,
            ManagedDocumentId = doc.Id,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });
        await db.SaveChangesAsync(cancellationToken);

        var reloaded = await db.DocumentDossiers.AsNoTracking()
            .Include(d => d.Items)
            .FirstAsync(d => d.Id == dossierId, cancellationToken);
        return MapDossier(reloaded);
    }

    public async Task<int> RefreshExpirationsAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsWrite);
        var now = clock.UtcNow;
        var expired = await db.ManagedDocuments
            .Where(d => d.IsActive && d.IsCurrent && d.Status == DocumentStatus.Current
                && d.ValidTo != null && d.ValidTo < now)
            .ToListAsync(cancellationToken);

        foreach (var doc in expired)
        {
            doc.Status = DocumentStatus.Expired;
            doc.IsCurrent = false;
            doc.UpdatedBy = currentUser.AccountId;
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var doc in expired)
        {
            var exists = await db.AppNotifications.AnyAsync(n =>
                n.TypeCode == "DOCUMENT_EXPIRED" && n.RelatedEntityId == doc.Id, cancellationToken);
            if (exists) continue;
            await notifications.PublishAsync(new PublishNotificationRequest(
                null,
                "Exspirácia dokumentu",
                $"Dokument „{doc.Title}“ exspiroval (verzia {doc.Version}).",
                "DOCUMENT_EXPIRED",
                "High",
                null,
                nameof(SystemRole.UnitAdministrator),
                null,
                nameof(ManagedDocument),
                doc.Id), cancellationToken);
        }

        return expired.Count;
    }

    public async Task<IReadOnlyList<FormTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsRead);
        var items = await db.FormTemplates.AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.IsActive && t.IsCurrent)
            .OrderBy(t => t.Code)
            .ToListAsync(cancellationToken);
        return items.Select(MapTemplate).ToList();
    }

    public async Task<FormTemplateDto> CreateTemplateAsync(CreateFormTemplateRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsWrite);
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.NameSk))
            throw new ValidationException("Kód a názov šablóny sú povinné.");
        if (request.Items is null || request.Items.Count == 0)
            throw new ValidationException("Šablóna musí mať aspoň jednu položku.");

        var existing = await db.FormTemplates
            .Where(t => t.Code == request.Code.Trim() && t.IsCurrent)
            .ToListAsync(cancellationToken);
        var version = 1;
        foreach (var old in existing)
        {
            old.IsCurrent = false;
            old.UpdatedBy = currentUser.AccountId;
            version = Math.Max(version, old.Version + 1);
        }

        var template = new FormTemplate
        {
            Code = request.Code.Trim(),
            NameSk = request.NameSk.Trim(),
            Kind = request.Kind,
            Version = version,
            IsCurrent = true,
            SupersedesTemplateId = existing.OrderByDescending(t => t.Version).FirstOrDefault()?.Id,
            DescriptionSk = request.DescriptionSk?.Trim(),
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        foreach (var item in request.Items.OrderBy(i => i.SortOrder))
        {
            template.Items.Add(new FormTemplateItem
            {
                Code = item.Code.Trim(),
                LabelSk = item.LabelSk.Trim(),
                ItemType = item.ItemType,
                IsRequired = item.IsRequired,
                IsBlocking = item.IsBlocking,
                SortOrder = item.SortOrder,
                OptionsJson = item.OptionsJson,
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            });
        }

        db.FormTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("FormTemplate.Create", nameof(FormTemplate), template.Id, null, MapTemplate(template), cancellationToken: cancellationToken);
        return MapTemplate(template);
    }

    public async Task<FormSubmissionDto> StartSubmissionAsync(StartFormSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");

        var template = await db.FormTemplates.Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == request.FormTemplateId && t.IsActive, cancellationToken)
            ?? throw new NotFoundException("Šablóna nebola nájdená.");

        var submission = new FormSubmission
        {
            FormTemplateId = template.Id,
            TemplateVersion = template.Version,
            LinkedEntityType = request.LinkedEntityType,
            LinkedEntityId = request.LinkedEntityId,
            Status = FormSubmissionStatus.Draft,
            FilledByAccountId = currentUser.AccountId.Value,
            IsComplete = false,
            HasBlockingIncomplete = template.Items.Any(i => i.IsBlocking && i.IsRequired),
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.FormSubmissions.Add(submission);
        await db.SaveChangesAsync(cancellationToken);
        return await MapSubmissionAsync(submission.Id, cancellationToken);
    }

    public async Task<FormSubmissionDto> SaveAnswersAsync(Guid submissionId, SaveFormAnswersRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsWrite);
        var submission = await db.FormSubmissions
            .Include(s => s.Answers)
            .Include(s => s.FormTemplate).ThenInclude(t => t.Items)
            .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken)
            ?? throw new NotFoundException("Vyplnený formulár nebol nájdený.");
        if (submission.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        if (submission.Status == FormSubmissionStatus.Completed)
            throw new ValidationException("Vyplnený formulár je už uzatvorený a viazaný na verziu šablóny.");

        var proposed = (request.Answers ?? []).Select(a => new FormAnswer
        {
            FormSubmissionId = submission.Id,
            FormTemplateItemId = a.FormTemplateItemId,
            ItemCode = a.ItemCode,
            ValueText = a.ValueText,
            ValueBool = a.ValueBool,
            ValueNumber = a.ValueNumber,
            ValueDate = a.ValueDate,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        }).ToList();

        var (isComplete, hasBlocking) = EvaluateCompleteness(submission.FormTemplate.Items, proposed);
        if (request.MarkCompleted)
        {
            if (hasBlocking)
                throw new ValidationException("Nie je možné dokončiť checklist — chýbajú blokujúce položky.");
            if (!isComplete)
                throw new ValidationException("Nie je možné dokončiť — chýbajú povinné položky.");
        }

        var existingAnswers = await db.FormAnswers.Where(a => a.FormSubmissionId == submission.Id).ToListAsync(cancellationToken);
        if (existingAnswers.Count > 0)
            db.FormAnswers.RemoveRange(existingAnswers);

        foreach (var answer in proposed)
            db.FormAnswers.Add(answer);

        submission.IsComplete = isComplete;
        submission.HasBlockingIncomplete = hasBlocking;
        if (request.MarkCompleted)
        {
            submission.Status = FormSubmissionStatus.Completed;
            submission.CompletedAt = clock.UtcNow;
        }

        submission.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("FormSubmission.Save", nameof(FormSubmission), submission.Id, null, null, cancellationToken: cancellationToken);
        return await MapSubmissionAsync(submission.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<FormSubmissionDto>> ListSubmissionsAsync(
        LinkedEntityType? entityType, Guid? entityId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsRead);
        var query = db.FormSubmissions.AsNoTracking()
            .Include(s => s.FormTemplate)
            .Include(s => s.Answers)
            .Where(s => s.IsActive);
        if (entityType is not null) query = query.Where(s => s.LinkedEntityType == entityType);
        if (entityId is not null) query = query.Where(s => s.LinkedEntityId == entityId);
        var items = await query.OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);
        return items.Select(s => MapSubmission(s)).ToList();
    }

    public async Task<RiskAssessmentDto> CreateRiskAssessmentAsync(CreateRiskAssessmentRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");

        var (score, classification) = RiskMethodology.Evaluate(request.Likelihood, request.Severity);
        var entity = new RiskAssessment
        {
            FormSubmissionId = request.FormSubmissionId,
            LinkedEntityType = request.LinkedEntityType,
            LinkedEntityId = request.LinkedEntityId,
            MethodologyCode = "LIKELIHOOD_SEVERITY_V1",
            Likelihood = request.Likelihood,
            Severity = request.Severity,
            Score = score,
            Classification = classification,
            Notes = request.Notes?.Trim(),
            AssessedByAccountId = currentUser.AccountId.Value,
            AssessedAt = clock.UtcNow,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.RiskAssessments.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("RiskAssessment.Create", nameof(RiskAssessment), entity.Id, null, MapRisk(entity), cancellationToken: cancellationToken);
        return MapRisk(entity);
    }

    public async Task<IReadOnlyList<RiskAssessmentDto>> ListRiskAssessmentsAsync(
        LinkedEntityType? entityType, Guid? entityId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsRead);
        var query = db.RiskAssessments.AsNoTracking().Where(r => r.IsActive);
        if (entityType is not null) query = query.Where(r => r.LinkedEntityType == entityType);
        if (entityId is not null) query = query.Where(r => r.LinkedEntityId == entityId);
        var items = await query.OrderByDescending(r => r.AssessedAt).ToListAsync(cancellationToken);
        return items.Select(MapRisk).ToList();
    }

    public async Task<DocumentBlockCheckDto> GetOperationDocumentBlockAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.DocumentsRead);
        await RefreshExpirationsInternalAsync(cancellationToken);

        var details = new List<string>();
        var docs = await db.ManagedDocuments.AsNoTracking()
            .Where(d => d.IsActive && d.LinkedEntityType == LinkedEntityType.Operation && d.LinkedEntityId == operationId)
            .ToListAsync(cancellationToken);

        var mandatoryMissingCurrent = docs
            .Where(d => d.IsMandatory)
            .GroupBy(d => d.DocumentTypeCode)
            .Where(g => !g.Any(d => d.IsCurrent && d.Status == DocumentStatus.Current
                && (d.ValidTo == null || d.ValidTo >= clock.UtcNow)))
            .Select(g => g.Key)
            .ToList();
        foreach (var code in mandatoryMissingCurrent)
            details.Add($"Chýba/neplatný povinný dokument typu {code}.");

        var blockingInvalid = docs.Where(d => d.BlocksWhenInvalid && d.IsCurrent
            && (d.Status == DocumentStatus.Expired || (d.ValidTo != null && d.ValidTo < clock.UtcNow))).ToList();
        foreach (var d in blockingInvalid)
            details.Add($"Neplatný dokument „{d.Title}“ blokuje operáciu.");

        var checklists = await db.FormSubmissions.AsNoTracking()
            .Include(s => s.FormTemplate)
            .Where(s => s.IsActive && s.LinkedEntityType == LinkedEntityType.Operation && s.LinkedEntityId == operationId
                && s.FormTemplate.Kind == FormTemplateKind.Checklist)
            .ToListAsync(cancellationToken);

        foreach (var c in checklists.Where(c => c.Status != FormSubmissionStatus.Completed || c.HasBlockingIncomplete || !c.IsComplete))
            details.Add($"Checklist „{c.FormTemplate.Code}“ v{c.TemplateVersion} nie je dokončený (blokujúce položky).");

        if (details.Count == 0)
            return new DocumentBlockCheckDto(false, "OK", details);
        return new DocumentBlockCheckDto(true, details[0], details);
    }

    private async Task RefreshExpirationsInternalAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var expired = await db.ManagedDocuments
            .Where(d => d.IsActive && d.IsCurrent && d.Status == DocumentStatus.Current
                && d.ValidTo != null && d.ValidTo < now)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0)
            return;

        foreach (var doc in expired)
        {
            doc.Status = DocumentStatus.Expired;
            doc.IsCurrent = false;
            doc.UpdatedBy = currentUser.AccountId;
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var doc in expired)
        {
            var exists = await db.AppNotifications.AnyAsync(n =>
                n.TypeCode == "DOCUMENT_EXPIRED" && n.RelatedEntityId == doc.Id, cancellationToken);
            if (exists) continue;
            await notifications.PublishAsync(new PublishNotificationRequest(
                null,
                "Exspirácia dokumentu",
                $"Dokument „{doc.Title}“ exspiroval (verzia {doc.Version}).",
                "DOCUMENT_EXPIRED",
                "High",
                null,
                nameof(SystemRole.UnitAdministrator),
                null,
                nameof(ManagedDocument),
                doc.Id), cancellationToken);
        }
    }

    private static (bool IsComplete, bool HasBlockingIncomplete) EvaluateCompleteness(
        IEnumerable<FormTemplateItem> items,
        IReadOnlyList<FormAnswer> answers)
    {
        var byItem = answers.ToDictionary(a => a.FormTemplateItemId);
        var incompleteRequired = false;
        var blockingIncomplete = false;
        foreach (var item in items.Where(i => i.IsActive))
        {
            byItem.TryGetValue(item.Id, out var answer);
            var filled = answer is not null && (
                !string.IsNullOrWhiteSpace(answer.ValueText)
                || answer.ValueBool is not null
                || answer.ValueNumber is not null
                || answer.ValueDate is not null);
            if (item.ItemType == FormItemType.YesNo && answer?.ValueBool is not null)
                filled = true;

            if (item.IsRequired && !filled)
            {
                incompleteRequired = true;
                if (item.IsBlocking)
                    blockingIncomplete = true;
            }
            else if (item.IsBlocking && item.ItemType == FormItemType.YesNo && answer?.ValueBool == false)
            {
                blockingIncomplete = true;
                incompleteRequired = true;
            }
        }

        return (!incompleteRequired, blockingIncomplete);
    }

    private static void RecalculateCompleteness(FormSubmission submission)
    {
        var (isComplete, hasBlocking) = EvaluateCompleteness(submission.FormTemplate.Items, submission.Answers.ToList());
        submission.IsComplete = isComplete;
        submission.HasBlockingIncomplete = hasBlocking;
    }

    private async Task<FormSubmissionDto> MapSubmissionAsync(Guid id, CancellationToken cancellationToken)
    {
        var s = await db.FormSubmissions.AsNoTracking()
            .Include(x => x.FormTemplate)
            .Include(x => x.Answers)
            .FirstAsync(x => x.Id == id, cancellationToken);
        return MapSubmission(s);
    }

    private static FormSubmissionDto MapSubmission(FormSubmission s) =>
        new(s.Id, s.FormTemplateId, s.FormTemplate.Code, s.TemplateVersion, s.LinkedEntityType, s.LinkedEntityId,
            s.Status, s.IsComplete, s.HasBlockingIncomplete,
            s.Answers.Select(a => new FormAnswerDto(a.FormTemplateItemId, a.ItemCode, a.ValueText, a.ValueBool, a.ValueNumber, a.ValueDate)).ToList(),
            s.RowVersion);

    private static FormTemplateDto MapTemplate(FormTemplate t) =>
        new(t.Id, t.Code, t.NameSk, t.Kind, t.Version, t.IsCurrent, t.DescriptionSk,
            t.Items.OrderBy(i => i.SortOrder).Select(i => new FormTemplateItemDto(
                i.Id, i.Code, i.LabelSk, i.ItemType, i.IsRequired, i.IsBlocking, i.SortOrder, i.OptionsJson)).ToList(),
            t.RowVersion);

    private static ManagedDocumentDto MapDocument(ManagedDocument d) =>
        new(d.Id, d.DocumentFamilyId, d.Title, d.DocumentTypeCode, d.LinkedEntityType, d.LinkedEntityId,
            d.Version, d.IsCurrent, d.Status, d.Sensitivity, d.OwnerAccountId, d.FileName, d.ContentType,
            d.SizeBytes, d.ContentHash, d.StoredFileId, d.StoredFileId is not null, d.RetentionRuleId,
            d.ValidFrom, d.ValidTo, d.IsMandatory, d.BlocksWhenInvalid,
            d.ScanStatus, d.IsActive, d.RowVersion);

    private static DocumentDossierDto MapDossier(DocumentDossier d) =>
        new(d.Id, d.Title, d.DescriptionSk, d.LinkedEntityType, d.LinkedEntityId,
            (d.Items ?? []).Where(i => i.IsActive).Select(i => i.ManagedDocumentId).ToList());

    private async Task<bool> CanAccessLinkedEntityAsync(LinkedEntityType type, Guid entityId, CancellationToken cancellationToken)
    {
        if (!policy.CanAccessBusinessData())
            return false;

        return type switch
        {
            LinkedEntityType.OrgUnit => await CheckOrgIdAsync(entityId, cancellationToken),
            LinkedEntityType.Uas => await CheckUasAsync(entityId, cancellationToken),
            LinkedEntityType.Pilot => await CheckPilotAsync(entityId, cancellationToken),
            LinkedEntityType.Operation => await CheckOperationAsync(entityId, cancellationToken),
            LinkedEntityType.Flight => await CheckFlightAsync(entityId, cancellationToken),
            LinkedEntityType.Battery => await CheckBatteryAsync(entityId, cancellationToken),
            _ => true
        };
    }

    private async Task<bool> CheckOrgIdAsync(Guid orgId, CancellationToken cancellationToken)
    {
        var org = await db.OrgUnits.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId, cancellationToken);
        return org is not null && policy.CanAccessOrgUnit(org.Path);
    }

    private async Task<bool> CheckUasAsync(Guid id, CancellationToken cancellationToken)
    {
        var uas = await db.UasAircraft.AsNoTracking().Include(u => u.OwningOrgUnit)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return uas is not null && (uas.OwningOrgUnit is null || policy.CanAccessOrgUnit(uas.OwningOrgUnit.Path));
    }

    private async Task<bool> CheckPilotAsync(Guid id, CancellationToken cancellationToken)
    {
        var pilot = await db.Pilots.AsNoTracking().Include(p => p.OrgUnit)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return pilot is not null && policy.CanAccessOrgUnit(pilot.OrgUnit.Path);
    }

    private async Task<bool> CheckOperationAsync(Guid id, CancellationToken cancellationToken)
    {
        var op = await db.Operations.AsNoTracking().Include(o => o.OrgUnit)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        return op is not null && policy.CanAccessOrgUnit(op.OrgUnit.Path);
    }

    private async Task<bool> CheckFlightAsync(Guid id, CancellationToken cancellationToken)
    {
        var flight = await db.Flights.AsNoTracking().Include(f => f.OrgUnit)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        return flight is not null && policy.CanAccessOrgUnit(flight.OrgUnit.Path);
    }

    private async Task<bool> CheckBatteryAsync(Guid id, CancellationToken cancellationToken)
    {
        var battery = await db.Batteries.AsNoTracking().Include(b => b.OwningOrgUnit)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        return battery is not null && (battery.OwningOrgUnit is null || policy.CanAccessOrgUnit(battery.OwningOrgUnit.Path));
    }

    private static RiskAssessmentDto MapRisk(RiskAssessment r) =>
        new(r.Id, r.LinkedEntityType, r.LinkedEntityId, r.MethodologyCode, r.Likelihood, r.Severity,
            r.Score, r.Classification, r.Notes, r.AssessedByAccountId, r.AssessedAt);
}
