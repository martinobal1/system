using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FullSchema_Modules63to610 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalFlightCount",
                table: "UasAircraft",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalFlightMinutes",
                table: "UasAircraft",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AppNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecipientAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientRoleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RelatedSafetyEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Defects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OriginalReport = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    OriginalReportLocked = table.Column<bool>(type: "boolean", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BlocksOperations = table.Column<bool>(type: "boolean", nullable: false),
                    TemporaryMeasure = table.Column<string>(type: "text", nullable: true),
                    ReportedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsibleAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedFlightId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedUasId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    TechnicalAssessment = table.Column<string>(type: "text", nullable: true),
                    TestPerformed = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DefectTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Defects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FormTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NameSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    SupersedesTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    DescriptionSk = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationConnectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NameSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ConnectorType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EndpointUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Format = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScheduleCron = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRunResult = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SecretRef = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationConnectors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenancePlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NameSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TriggerType = table.Column<int>(type: "integer", nullable: false),
                    IntervalValue = table.Column<int>(type: "integer", nullable: true),
                    NextDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextDueCounter = table.Column<int>(type: "integer", nullable: true),
                    WarningThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    BlockWhenOverdue = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentCounter = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenancePlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManagedDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentFamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DocumentTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LinkedEntityType = table.Column<int>(type: "integer", nullable: false),
                    LinkedEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    SupersedesDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Sensitivity = table.Column<int>(type: "integer", nullable: false),
                    OwnerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    BlocksWhenInvalid = table.Column<bool>(type: "boolean", nullable: false),
                    ScanStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfficialReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TitleSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ReportFamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupersedesReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    DataAsOf = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FiltersJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RulesJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PreparedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovalReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OperationTypeCode = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlannedEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LocationAddress = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LocationLat = table.Column<double>(type: "double precision", nullable: true),
                    LocationLon = table.Column<double>(type: "double precision", nullable: true),
                    LocationGeoJson = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsUrgent = table.Column<bool>(type: "boolean", nullable: false),
                    UrgentReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsSubmittedVersionLocked = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Operations_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pilots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrencyValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFlightAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pilots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pilots_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pilots_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormSubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedEntityType = table.Column<int>(type: "integer", nullable: false),
                    LinkedEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    MethodologyCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Likelihood = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Classification = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    AssessedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAssessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InitialReport = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    InitialReportLocked = table.Column<bool>(type: "boolean", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReportedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedFlightId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedUasId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LocationDescription = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Findings = table.Column<string>(type: "text", nullable: true),
                    Causes = table.Column<string>(type: "text", nullable: true),
                    ContributingFactors = table.Column<string>(type: "text", nullable: true),
                    Recommendations = table.Column<string>(type: "text", nullable: true),
                    ClosureNotes = table.Column<string>(type: "text", nullable: true),
                    ClosureExceptionReason = table.Column<string>(type: "text", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AutoSuspendApplied = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssigneeAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TaskTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FormSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    LinkedEntityType = table.Column<int>(type: "integer", nullable: false),
                    LinkedEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FilledByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                    HasBlockingIncomplete = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormSubmissions_FormTemplates_FormTemplateId",
                        column: x => x.FormTemplateId,
                        principalTable: "FormTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LabelSk = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsBlocking = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    OptionsJson = table.Column<string>(type: "text", nullable: true),
                    VisibleWhenJson = table.Column<string>(type: "text", nullable: true),
                    ValidationJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormTemplateItems_FormTemplates_FormTemplateId",
                        column: x => x.FormTemplateId,
                        principalTable: "FormTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenancePlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedTechnicianAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrders_MaintenancePlans_MaintenancePlanId",
                        column: x => x.MaintenancePlanId,
                        principalTable: "MaintenancePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OperationResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<int>(type: "integer", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationResources_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationStatusHistories_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Flights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandaloneReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    UasId = table.Column<Guid>(type: "uuid", nullable: true),
                    PilotId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatteryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TakeoffAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LandingAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    DistanceMeters = table.Column<double>(type: "double precision", nullable: true),
                    MaxAltitudeMeters = table.Column<double>(type: "double precision", nullable: true),
                    MaxSpeedMps = table.Column<double>(type: "double precision", nullable: true),
                    PostFlightNotes = table.Column<string>(type: "text", nullable: true),
                    ChecklistResult = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DeviationNotes = table.Column<string>(type: "text", nullable: true),
                    CountersApplied = table.Column<bool>(type: "boolean", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackGeoJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Flights_Batteries_BatteryId",
                        column: x => x.BatteryId,
                        principalTable: "Batteries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Flights_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Flights_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Flights_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Flights_UasAircraft_UasId",
                        column: x => x.UasId,
                        principalTable: "UasAircraft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PilotCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PilotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TitleSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExternalNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SupersedesCertificateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PilotCertificates_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PilotQualifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PilotId = table.Column<Guid>(type: "uuid", nullable: false),
                    QualificationCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NameSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Issuer = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotQualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PilotQualifications_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PilotStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PilotId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PilotStatusHistories_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PilotTrainings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PilotId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TitleSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    InstructorName = table.Column<string>(type: "text", nullable: true),
                    ValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotTrainings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PilotTrainings_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SafetyEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    OwnerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CompletionEvidence = table.Column<string>(type: "text", nullable: true),
                    VerifiedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WaiverReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_SafetyEvents_SafetyEventId",
                        column: x => x.SafetyEventId,
                        principalTable: "SafetyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEvidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SafetyEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SupersedesEvidenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyEvidences_SafetyEvents_SafetyEventId",
                        column: x => x.SafetyEventId,
                        principalTable: "SafetyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FormAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormTemplateItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ValueText = table.Column<string>(type: "text", nullable: true),
                    ValueBool = table.Column<bool>(type: "boolean", nullable: true),
                    ValueNumber = table.Column<decimal>(type: "numeric", nullable: true),
                    ValueDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormAnswers_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkScope = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Result = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PerformedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PerformedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartsUsed = table.Column<string>(type: "text", nullable: true),
                    DocumentationNotes = table.Column<string>(type: "text", nullable: true),
                    AirworthinessRestored = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceRecords_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlightTrackPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlightId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    AltitudeMeters = table.Column<double>(type: "double precision", nullable: true),
                    SpeedMps = table.Column<double>(type: "double precision", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightTrackPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightTrackPoints_Flights_FlightId",
                        column: x => x.FlightId,
                        principalTable: "Flights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProcessingMessage = table.Column<string>(type: "text", nullable: true),
                    FlightId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchedUasId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchedOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchedPilotId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchedBatteryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OriginalPayload = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelemetryImports_Flights_FlightId",
                        column: x => x.FlightId,
                        principalTable: "Flights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_IsActive",
                table: "AppNotifications",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_Status",
                table: "AppNotifications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_TypeCode_RelatedSafetyEventId_RecipientRol~",
                table: "AppNotifications",
                columns: new[] { "TypeCode", "RelatedSafetyEventId", "RecipientRoleCode" });

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_IsActive",
                table: "CorrectiveActions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_SafetyEventId",
                table: "CorrectiveActions",
                column: "SafetyEventId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_Status",
                table: "CorrectiveActions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Defects_DefectTypeCode",
                table: "Defects",
                column: "DefectTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_Defects_IsActive",
                table: "Defects",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Defects_TargetType_TargetId",
                table: "Defects",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Flights_BatteryId",
                table: "Flights",
                column: "BatteryId");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_IsActive",
                table: "Flights",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_OperationId",
                table: "Flights",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_OrgUnitId",
                table: "Flights",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_PilotId",
                table: "Flights",
                column: "PilotId");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_Status",
                table: "Flights",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_UasId",
                table: "Flights",
                column: "UasId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightTrackPoints_FlightId_Sequence",
                table: "FlightTrackPoints",
                columns: new[] { "FlightId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_FlightTrackPoints_IsActive",
                table: "FlightTrackPoints",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FormAnswers_FormSubmissionId",
                table: "FormAnswers",
                column: "FormSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormAnswers_IsActive",
                table: "FormAnswers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_FormTemplateId",
                table: "FormSubmissions",
                column: "FormTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_IsActive",
                table: "FormSubmissions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_LinkedEntityType_LinkedEntityId",
                table: "FormSubmissions",
                columns: new[] { "LinkedEntityType", "LinkedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplateItems_FormTemplateId",
                table: "FormTemplateItems",
                column: "FormTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplateItems_IsActive",
                table: "FormTemplateItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_Code_Version",
                table: "FormTemplates",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_IsActive",
                table: "FormTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationConnectors_Code",
                table: "IntegrationConnectors",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationConnectors_IsActive",
                table: "IntegrationConnectors",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_IsActive",
                table: "MaintenancePlans",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_TargetType_TargetId",
                table: "MaintenancePlans",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_IsActive",
                table: "MaintenanceRecords",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_WorkOrderId",
                table: "MaintenanceRecords",
                column: "WorkOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocuments_DocumentFamilyId",
                table: "ManagedDocuments",
                column: "DocumentFamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocuments_DocumentTypeCode",
                table: "ManagedDocuments",
                column: "DocumentTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocuments_IsActive",
                table: "ManagedDocuments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocuments_LinkedEntityType_LinkedEntityId_IsCurrent",
                table: "ManagedDocuments",
                columns: new[] { "LinkedEntityType", "LinkedEntityId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialReports_IsActive",
                table: "OfficialReports",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OfficialReports_ReportFamilyId",
                table: "OfficialReports",
                column: "ReportFamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficialReports_ReportTypeCode_Status",
                table: "OfficialReports",
                columns: new[] { "ReportTypeCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationResources_IsActive",
                table: "OperationResources",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OperationResources_OperationId",
                table: "OperationResources",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationResources_ResourceType_ResourceId",
                table: "OperationResources",
                columns: new[] { "ResourceType", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Operations_IsActive",
                table: "Operations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Operations_OrgUnitId",
                table: "Operations",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Operations_PlannedStart_PlannedEnd",
                table: "Operations",
                columns: new[] { "PlannedStart", "PlannedEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_Operations_Status",
                table: "Operations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OperationStatusHistories_IsActive",
                table: "OperationStatusHistories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OperationStatusHistories_OperationId",
                table: "OperationStatusHistories",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_PilotCertificates_IsActive",
                table: "PilotCertificates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PilotCertificates_PilotId_CertificateTypeCode_Version",
                table: "PilotCertificates",
                columns: new[] { "PilotId", "CertificateTypeCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PilotQualifications_IsActive",
                table: "PilotQualifications",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PilotQualifications_PilotId",
                table: "PilotQualifications",
                column: "PilotId");

            migrationBuilder.CreateIndex(
                name: "IX_Pilots_IsActive",
                table: "Pilots",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Pilots_OrgUnitId",
                table: "Pilots",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Pilots_PersonId",
                table: "Pilots",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Pilots_PersonId_IsActive",
                table: "Pilots",
                columns: new[] { "PersonId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotStatusHistories_IsActive",
                table: "PilotStatusHistories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PilotStatusHistories_PilotId",
                table: "PilotStatusHistories",
                column: "PilotId");

            migrationBuilder.CreateIndex(
                name: "IX_PilotTrainings_IsActive",
                table: "PilotTrainings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PilotTrainings_PilotId",
                table: "PilotTrainings",
                column: "PilotId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_IsActive",
                table: "RiskAssessments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_LinkedEntityType_LinkedEntityId",
                table: "RiskAssessments",
                columns: new[] { "LinkedEntityType", "LinkedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_IsActive",
                table: "SafetyEvents",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_RelatedUasId",
                table: "SafetyEvents",
                column: "RelatedUasId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_Severity",
                table: "SafetyEvents",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_Status",
                table: "SafetyEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvidences_IsActive",
                table: "SafetyEvidences",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvidences_SafetyEventId_IsCurrent",
                table: "SafetyEvidences",
                columns: new[] { "SafetyEventId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryImports_ContentHash",
                table: "TelemetryImports",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryImports_FlightId",
                table: "TelemetryImports",
                column: "FlightId");

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryImports_IsActive",
                table: "TelemetryImports",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_IsActive",
                table: "WorkOrders",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_MaintenancePlanId",
                table: "WorkOrders",
                column: "MaintenancePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_IsActive",
                table: "WorkTasks",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_RelatedOperationId",
                table: "WorkTasks",
                column: "RelatedOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_Status",
                table: "WorkTasks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppNotifications");

            migrationBuilder.DropTable(
                name: "CorrectiveActions");

            migrationBuilder.DropTable(
                name: "Defects");

            migrationBuilder.DropTable(
                name: "FlightTrackPoints");

            migrationBuilder.DropTable(
                name: "FormAnswers");

            migrationBuilder.DropTable(
                name: "FormTemplateItems");

            migrationBuilder.DropTable(
                name: "IntegrationConnectors");

            migrationBuilder.DropTable(
                name: "MaintenanceRecords");

            migrationBuilder.DropTable(
                name: "ManagedDocuments");

            migrationBuilder.DropTable(
                name: "OfficialReports");

            migrationBuilder.DropTable(
                name: "OperationResources");

            migrationBuilder.DropTable(
                name: "OperationStatusHistories");

            migrationBuilder.DropTable(
                name: "PilotCertificates");

            migrationBuilder.DropTable(
                name: "PilotQualifications");

            migrationBuilder.DropTable(
                name: "PilotStatusHistories");

            migrationBuilder.DropTable(
                name: "PilotTrainings");

            migrationBuilder.DropTable(
                name: "RiskAssessments");

            migrationBuilder.DropTable(
                name: "SafetyEvidences");

            migrationBuilder.DropTable(
                name: "TelemetryImports");

            migrationBuilder.DropTable(
                name: "WorkTasks");

            migrationBuilder.DropTable(
                name: "FormSubmissions");

            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropTable(
                name: "SafetyEvents");

            migrationBuilder.DropTable(
                name: "Flights");

            migrationBuilder.DropTable(
                name: "FormTemplates");

            migrationBuilder.DropTable(
                name: "MaintenancePlans");

            migrationBuilder.DropTable(
                name: "Operations");

            migrationBuilder.DropTable(
                name: "Pilots");

            migrationBuilder.DropColumn(
                name: "TotalFlightCount",
                table: "UasAircraft");

            migrationBuilder.DropColumn(
                name: "TotalFlightMinutes",
                table: "UasAircraft");
        }
    }
}
