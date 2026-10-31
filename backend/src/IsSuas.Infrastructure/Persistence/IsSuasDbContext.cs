using IsSuas.Domain.Common;
using IsSuas.Domain.Controls;
using IsSuas.Domain.Documents;
using IsSuas.Domain.Fleet;
using IsSuas.Domain.Flights;
using IsSuas.Domain.GeoZones;
using IsSuas.Domain.Identity;
using IsSuas.Domain.Integrations;
using IsSuas.Domain.Maintenance;
using IsSuas.Domain.Notifications;
using IsSuas.Domain.Operations;
using IsSuas.Domain.Pilots;
using IsSuas.Domain.Reporting;
using IsSuas.Domain.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IsSuas.Infrastructure.Persistence;

public class IsSuasDbContext(DbContextOptions<IsSuasDbContext> options) : DbContext(options)
{
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<OrgUnitMoveHistory> OrgUnitMoveHistories => Set<OrgUnitMoveHistory>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<DelegatedPermission> DelegatedPermissions => Set<DelegatedPermission>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<ControlAudit> ControlAudits => Set<ControlAudit>();
    public DbSet<ControlTeamMember> ControlTeamMembers => Set<ControlTeamMember>();
    public DbSet<ControlChecklistItem> ControlChecklistItems => Set<ControlChecklistItem>();
    public DbSet<ControlFinding> ControlFindings => Set<ControlFinding>();
    public DbSet<ControlCorrectiveAction> ControlCorrectiveActions => Set<ControlCorrectiveAction>();
    public DbSet<ControlReport> ControlReports => Set<ControlReport>();
    public DbSet<LookupCode> LookupCodes => Set<LookupCode>();
    public DbSet<LookupItem> LookupItems => Set<LookupItem>();
    public DbSet<ConfigRule> ConfigRules => Set<ConfigRule>();
    public DbSet<Uas> UasAircraft => Set<Uas>();
    public DbSet<UasAssignment> UasAssignments => Set<UasAssignment>();
    public DbSet<UasTypeDoc> UasTypeDocs => Set<UasTypeDoc>();
    public DbSet<UasStatusHistory> UasStatusHistories => Set<UasStatusHistory>();
    public DbSet<Component> Components => Set<Component>();
    public DbSet<Installation> Installations => Set<Installation>();
    public DbSet<ComponentCompatibility> ComponentCompatibilities => Set<ComponentCompatibility>();
    public DbSet<Battery> Batteries => Set<Battery>();
    public DbSet<BatteryAssignment> BatteryAssignments => Set<BatteryAssignment>();
    public DbSet<BatteryCompatibility> BatteryCompatibilities => Set<BatteryCompatibility>();
    public DbSet<BatteryTechnicalRecord> BatteryTechnicalRecords => Set<BatteryTechnicalRecord>();
    public DbSet<BatteryOperationRecord> BatteryOperationRecords => Set<BatteryOperationRecord>();
    public DbSet<BatteryStatusHistory> BatteryStatusHistories => Set<BatteryStatusHistory>();
    public DbSet<Pilot> Pilots => Set<Pilot>();
    public DbSet<PilotQualification> PilotQualifications => Set<PilotQualification>();
    public DbSet<PilotCertificate> PilotCertificates => Set<PilotCertificate>();
    public DbSet<PilotTraining> PilotTrainings => Set<PilotTraining>();
    public DbSet<PilotStatusHistory> PilotStatusHistories => Set<PilotStatusHistory>();
    public DbSet<PersonFunctionAssignment> PersonFunctionAssignments => Set<PersonFunctionAssignment>();
    public DbSet<PilotRestriction> PilotRestrictions => Set<PilotRestriction>();
    public DbSet<QualificationDocumentVerification> QualificationDocumentVerifications => Set<QualificationDocumentVerification>();
    public DbSet<CertificateScope> CertificateScopes => Set<CertificateScope>();
    public DbSet<TrainingCatalogItem> TrainingCatalogItems => Set<TrainingCatalogItem>();
    public DbSet<TrainingActivity> TrainingActivities => Set<TrainingActivity>();
    public DbSet<TrainingParticipation> TrainingParticipations => Set<TrainingParticipation>();
    public DbSet<TrainingActivityResult> TrainingActivityResults => Set<TrainingActivityResult>();
    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<OperationResource> OperationResources => Set<OperationResource>();
    public DbSet<OperationStatusHistory> OperationStatusHistories => Set<OperationStatusHistory>();
    public DbSet<OperationApprovalStep> OperationApprovalSteps => Set<OperationApprovalStep>();
    public DbSet<OperationDecision> OperationDecisions => Set<OperationDecision>();
    public DbSet<OperationApprovalCondition> OperationApprovalConditions => Set<OperationApprovalCondition>();
    public DbSet<OperationRecurrenceRule> OperationRecurrenceRules => Set<OperationRecurrenceRule>();
    public DbSet<OperationMapCheck> OperationMapChecks => Set<OperationMapCheck>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<FlightTrackPoint> FlightTrackPoints => Set<FlightTrackPoint>();
    public DbSet<TelemetryImport> TelemetryImports => Set<TelemetryImport>();
    public DbSet<MaintenancePlan> MaintenancePlans => Set<MaintenancePlan>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<Defect> Defects => Set<Defect>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<TechnicalRestriction> TechnicalRestrictions => Set<TechnicalRestriction>();
    public DbSet<ReleaseToService> ReleasesToService => Set<ReleaseToService>();
    public DbSet<TechnicalStatusHistory> TechnicalStatusHistories => Set<TechnicalStatusHistory>();
    public DbSet<SafetyEvent> SafetyEvents => Set<SafetyEvent>();
    public DbSet<SafetyEvidence> SafetyEvidences => Set<SafetyEvidence>();
    public DbSet<CorrectiveAction> CorrectiveActions => Set<CorrectiveAction>();
    public DbSet<SafetyEventParticipant> SafetyEventParticipants => Set<SafetyEventParticipant>();
    public DbSet<SafetyEventAsset> SafetyEventAssets => Set<SafetyEventAsset>();
    public DbSet<SafetyImmediateAction> SafetyImmediateActions => Set<SafetyImmediateAction>();
    public DbSet<SafetyInvestigation> SafetyInvestigations => Set<SafetyInvestigation>();
    public DbSet<SafetyCause> SafetyCauses => Set<SafetyCause>();
    public DbSet<SafetyReportingObligation> SafetyReportingObligations => Set<SafetyReportingObligation>();
    public DbSet<SafetyStatusHistory> SafetyStatusHistories => Set<SafetyStatusHistory>();
    public DbSet<SafetyClassificationHistory> SafetyClassificationHistories => Set<SafetyClassificationHistory>();
    public DbSet<FleetSafetyAlert> FleetSafetyAlerts => Set<FleetSafetyAlert>();
    public DbSet<AppNotification> AppNotifications => Set<AppNotification>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
    public DbSet<WorkTaskHistory> WorkTaskHistories => Set<WorkTaskHistory>();
    public DbSet<ManagedDocument> ManagedDocuments => Set<ManagedDocument>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<RetentionRule> RetentionRules => Set<RetentionRule>();
    public DbSet<DocumentDossier> DocumentDossiers => Set<DocumentDossier>();
    public DbSet<DocumentDossierItem> DocumentDossierItems => Set<DocumentDossierItem>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<FormTemplateItem> FormTemplateItems => Set<FormTemplateItem>();
    public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();
    public DbSet<FormAnswer> FormAnswers => Set<FormAnswer>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<OfficialReport> OfficialReports => Set<OfficialReport>();
    public DbSet<PilotUasAuthorization> PilotUasAuthorizations => Set<PilotUasAuthorization>();
    public DbSet<MonthlyFlightReport> MonthlyFlightReports => Set<MonthlyFlightReport>();
    public DbSet<MonthlyFlightReportLine> MonthlyFlightReportLines => Set<MonthlyFlightReportLine>();
    public DbSet<MonthlyFlightReportChangeLog> MonthlyFlightReportChangeLogs => Set<MonthlyFlightReportChangeLog>();
    public DbSet<GeoZoneImport> GeoZoneImports => Set<GeoZoneImport>();
    public DbSet<GeoZone> GeoZones => Set<GeoZone>();
    public DbSet<IntegrationConnector> IntegrationConnectors => Set<IntegrationConnector>();
    public DbSet<TrashEntry> TrashEntries => Set<TrashEntry>();

    /// <summary>
    /// True = zobraziť aj záznamy v koši (používa kôš a seeder). Predvolene sú záznamy v koši všade skryté.
    /// </summary>
    public bool IgnoreTrashFilter { get; set; }

    private bool _allowPhysicalDelete;

    /// <summary>
    /// Jediná výnimka z pravidla „nič fyzicky nemazať“: trvalé vymazanie z koša (iba gestor).
    /// Použitie: using (db.AllowPhysicalDelete()) { ...; await db.SaveChangesAsync(); }
    /// </summary>
    public IDisposable AllowPhysicalDelete()
    {
        _allowPhysicalDelete = true;
        return new PhysicalDeleteScope(this);
    }

    private sealed class PhysicalDeleteScope(IsSuasDbContext db) : IDisposable
    {
        public void Dispose() => db._allowPhysicalDelete = false;
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            configurationBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetToBinaryConverter>();
            configurationBuilder.Properties<DateTimeOffset?>()
                .HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IsSuasDbContext).Assembly);

        // Kôš: entita s otvoreným záznamom v koši je skrytá vo všetkých dotazoch (zoznamy, detail, hľadanie, prehľady).
        AddTrashFilter<Pilot>(modelBuilder, nameof(Pilot));
        AddTrashFilter<PilotCertificate>(modelBuilder, nameof(PilotCertificate));
        AddTrashFilter<Flight>(modelBuilder, nameof(Flight));
        AddTrashFilter<Operation>(modelBuilder, nameof(Operation));
        AddTrashFilter<Uas>(modelBuilder, nameof(Uas));
        AddTrashFilter<Defect>(modelBuilder, nameof(Defect));
        AddTrashFilter<SafetyEvent>(modelBuilder, nameof(SafetyEvent));
        AddTrashFilter<Account>(modelBuilder, nameof(Account));

        base.OnModelCreating(modelBuilder);
    }

    private void AddTrashFilter<T>(ModelBuilder modelBuilder, string entityType) where T : AuditableEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(e =>
            IgnoreTrashFilter
            || !TrashEntries.Any(t => t.EntityType == entityType
                && t.EntityId == e.Id
                && t.RestoredAt == null
                && t.PurgedAt == null));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Soft-delete rule: never physically delete auditable business entities (výnimka: trvalé vymazanie z koša).
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Deleted && !_allowPhysicalDelete)
                throw new InvalidOperationException(
                    $"Fyzické mazanie entity {entry.Entity.GetType().Name} je zakázané. Použite deaktiváciu (IsActive=false).");
        }

        // Audit is append-only — even system administrator must not modify/delete.
        foreach (var entry in ChangeTracker.Entries<AuditEvent>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Auditná stopa je nemenná (append-only).");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.RowVersion++;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
