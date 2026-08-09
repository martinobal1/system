using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Atlas_Wave9_SafetyDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FleetSafetyAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SafetyEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SeverityCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScopeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AffectedUasTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    WithdrawalReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetSafetyAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetSafetyAlerts_SafetyEvents_SafetyEventId",
                        column: x => x.SafetyEventId,
                        principalTable: "SafetyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SafetyClassificationHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SafetyEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassificationTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviousValueCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NewValueCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChangedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyClassificationHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyClassificationHistories_SafetyEvents_SafetyEventId",
                        column: x => x.SafetyEventId,
                        principalTable: "SafetyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEventAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SafetyEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UasId = table.Column<Guid>(type: "uuid", nullable: true),
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatteryId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetRoleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DamageStatusCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DamageDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TechnicalStatusAfterEvent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SecuredAsEvidence = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEventAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyEventAssets_Batteries_BatteryId",
                        column: x => x.BatteryId,
                        principalTable: "Batteries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyEventAssets_Components_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "Components",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyEventAssets_SafetyEvents_SafetyEventId",
                        column: x => x.SafetyEventId,
                        principalTable: "SafetyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SafetyEventAssets_UasAircraft_UasId",
                        column: x => x.UasId,
                        principalTable: "UasAircraft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEventParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SafetyEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantRoleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InvolvementDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InjuryStatusCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StatementObtained = table.Column<bool>(type: "boolean", nullable: false),
                    NotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEventParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyEventParticipants_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyEventParticipants_SafetyEvents_SafetyEventId",
                        column: x => x.SafetyEventId,
                        principalTable: "SafetyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SafetyImmediateActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SafetyEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActionDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PerformedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PerformedByPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUpRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyImmediateActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyImmediateActions_Persons_PerformedByPersonId",
                        column: x => x.PerformedByPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyImmediateActions_SafetyEvents_SafetyEventId",
                        column: x => x.SafetyEventId,
                        principalTable: "SafetyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SafetyInvestigations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SafetyEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestigationNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InvestigationTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponsiblePersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlannedCompletionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FindingsSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Conclusion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    InterruptionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyInvestigations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyInvestigations_Persons_ResponsiblePersonId",
                        column: x => x.ResponsiblePersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyInvestigations_SafetyEvents_SafetyEventId",
                        column: x => x.SafetyEventId,
                        principalTable: "SafetyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SafetyReportingObligations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SafetyEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObligationTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecipientAuthority = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LegalBasis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReportingDeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReportedByPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalReferenceNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DelayReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyReportingObligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyReportingObligations_Persons_ReportedByPersonId",
                        column: x => x.ReportedByPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyReportingObligations_SafetyEvents_SafetyEventId",
                        column: x => x.SafetyEventId,
                        principalTable: "SafetyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SafetyStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SafetyEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<int>(type: "integer", nullable: true),
                    NewStatus = table.Column<int>(type: "integer", nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChangedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyStatusHistories_SafetyEvents_SafetyEventId",
                        column: x => x.SafetyEventId,
                        principalTable: "SafetyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SafetyCauses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestigationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CauseCategoryCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CauseDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsRootCause = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConfidenceLevelCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SupportingEvidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmedByPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyCauses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyCauses_Persons_ConfirmedByPersonId",
                        column: x => x.ConfirmedByPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyCauses_SafetyInvestigations_InvestigationId",
                        column: x => x.InvestigationId,
                        principalTable: "SafetyInvestigations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FleetSafetyAlerts_AlertNumber",
                table: "FleetSafetyAlerts",
                column: "AlertNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FleetSafetyAlerts_IsActive",
                table: "FleetSafetyAlerts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FleetSafetyAlerts_SafetyEventId",
                table: "FleetSafetyAlerts",
                column: "SafetyEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyCauses_ConfirmedByPersonId",
                table: "SafetyCauses",
                column: "ConfirmedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyCauses_InvestigationId",
                table: "SafetyCauses",
                column: "InvestigationId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyCauses_IsActive",
                table: "SafetyCauses",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyClassificationHistories_IsActive",
                table: "SafetyClassificationHistories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyClassificationHistories_SafetyEventId",
                table: "SafetyClassificationHistories",
                column: "SafetyEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventAssets_BatteryId",
                table: "SafetyEventAssets",
                column: "BatteryId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventAssets_ComponentId",
                table: "SafetyEventAssets",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventAssets_IsActive",
                table: "SafetyEventAssets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventAssets_SafetyEventId",
                table: "SafetyEventAssets",
                column: "SafetyEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventAssets_UasId",
                table: "SafetyEventAssets",
                column: "UasId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventParticipants_IsActive",
                table: "SafetyEventParticipants",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventParticipants_PersonId",
                table: "SafetyEventParticipants",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventParticipants_SafetyEventId",
                table: "SafetyEventParticipants",
                column: "SafetyEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyImmediateActions_IsActive",
                table: "SafetyImmediateActions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyImmediateActions_PerformedByPersonId",
                table: "SafetyImmediateActions",
                column: "PerformedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyImmediateActions_SafetyEventId",
                table: "SafetyImmediateActions",
                column: "SafetyEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyInvestigations_InvestigationNumber",
                table: "SafetyInvestigations",
                column: "InvestigationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyInvestigations_IsActive",
                table: "SafetyInvestigations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyInvestigations_ResponsiblePersonId",
                table: "SafetyInvestigations",
                column: "ResponsiblePersonId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyInvestigations_SafetyEventId",
                table: "SafetyInvestigations",
                column: "SafetyEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyReportingObligations_IsActive",
                table: "SafetyReportingObligations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyReportingObligations_ReportedByPersonId",
                table: "SafetyReportingObligations",
                column: "ReportedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyReportingObligations_SafetyEventId",
                table: "SafetyReportingObligations",
                column: "SafetyEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyStatusHistories_IsActive",
                table: "SafetyStatusHistories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyStatusHistories_SafetyEventId",
                table: "SafetyStatusHistories",
                column: "SafetyEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FleetSafetyAlerts");

            migrationBuilder.DropTable(
                name: "SafetyCauses");

            migrationBuilder.DropTable(
                name: "SafetyClassificationHistories");

            migrationBuilder.DropTable(
                name: "SafetyEventAssets");

            migrationBuilder.DropTable(
                name: "SafetyEventParticipants");

            migrationBuilder.DropTable(
                name: "SafetyImmediateActions");

            migrationBuilder.DropTable(
                name: "SafetyReportingObligations");

            migrationBuilder.DropTable(
                name: "SafetyStatusHistories");

            migrationBuilder.DropTable(
                name: "SafetyInvestigations");
        }
    }
}
