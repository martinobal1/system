using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Atlas_Wave8_PilotCompetence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TrainingParticipationId",
                table: "PilotTrainings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CertificateScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UasTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OperationTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FunctionTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RestrictionText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_CertificateScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateScopes_PilotCertificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "PilotCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonFunctionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FunctionTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationOrgUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentScopeTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UasId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TerminationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonFunctionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonFunctionAssignments_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonFunctionAssignments_OrgUnits_OrganizationOrgUnitId",
                        column: x => x.OrganizationOrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonFunctionAssignments_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonFunctionAssignments_UasAircraft_UasId",
                        column: x => x.UasId,
                        principalTable: "UasAircraft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PilotRestrictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PilotId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestrictionTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SeverityCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BlocksOperation = table.Column<bool>(type: "boolean", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImposedByPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TerminationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotRestrictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PilotRestrictions_Persons_ImposedByPersonId",
                        column: x => x.ImposedByPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PilotRestrictions_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QualificationDocumentVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QualificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VerificationMethodCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    VerifiedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifiedSource = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    NextVerificationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_QualificationDocumentVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualificationDocumentVerifications_PilotQualifications_Qual~",
                        column: x => x.QualificationId,
                        principalTable: "PilotQualifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingCatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActivityName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ActivityTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    MinimumDurationHours = table.Column<decimal>(type: "numeric", nullable: true),
                    RecurrencePeriodMonths = table.Column<int>(type: "integer", nullable: true),
                    ExaminationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    PassingScorePercent = table.Column<int>(type: "integer", nullable: true),
                    PracticalPartRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_TrainingCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationOrgUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedStartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlannedEndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualStartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActualEndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Location = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DeliveryFormCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InstructorPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExaminerPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaximumParticipants = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingActivities_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingActivities_OrgUnits_OrganizationOrgUnitId",
                        column: x => x.OrganizationOrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingActivities_Persons_ExaminerPersonId",
                        column: x => x.ExaminerPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingActivities_Persons_InstructorPersonId",
                        column: x => x.InstructorPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingActivities_TrainingCatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "TrainingCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingParticipations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipationRoleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RegistrationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RegistrationStatus = table.Column<int>(type: "integer", nullable: false),
                    AttendanceStatus = table.Column<int>(type: "integer", nullable: true),
                    AttendancePercentage = table.Column<int>(type: "integer", nullable: true),
                    CompletionStatus = table.Column<int>(type: "integer", nullable: true),
                    AbsenceReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_TrainingParticipations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingParticipations_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingParticipations_TrainingActivities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "TrainingActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingActivityResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    TheoreticalScorePercent = table.Column<int>(type: "integer", nullable: true),
                    PracticalScorePercent = table.Column<int>(type: "integer", nullable: true),
                    OverallScorePercent = table.Column<int>(type: "integer", nullable: true),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EvaluatedByPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_TrainingActivityResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingActivityResults_Persons_EvaluatedByPersonId",
                        column: x => x.EvaluatedByPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingActivityResults_TrainingParticipations_Participatio~",
                        column: x => x.ParticipationId,
                        principalTable: "TrainingParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PilotTrainings_TrainingParticipationId",
                table: "PilotTrainings",
                column: "TrainingParticipationId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateScopes_CertificateId",
                table: "CertificateScopes",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateScopes_IsActive",
                table: "CertificateScopes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PersonFunctionAssignments_IsActive",
                table: "PersonFunctionAssignments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PersonFunctionAssignments_OrganizationOrgUnitId",
                table: "PersonFunctionAssignments",
                column: "OrganizationOrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonFunctionAssignments_OrgUnitId",
                table: "PersonFunctionAssignments",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonFunctionAssignments_PersonId_FunctionTypeCode_Status",
                table: "PersonFunctionAssignments",
                columns: new[] { "PersonId", "FunctionTypeCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonFunctionAssignments_UasId",
                table: "PersonFunctionAssignments",
                column: "UasId");

            migrationBuilder.CreateIndex(
                name: "IX_PilotRestrictions_ImposedByPersonId",
                table: "PilotRestrictions",
                column: "ImposedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PilotRestrictions_IsActive",
                table: "PilotRestrictions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PilotRestrictions_PilotId_Status",
                table: "PilotRestrictions",
                columns: new[] { "PilotId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QualificationDocumentVerifications_IsActive",
                table: "QualificationDocumentVerifications",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_QualificationDocumentVerifications_QualificationId",
                table: "QualificationDocumentVerifications",
                column: "QualificationId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingActivities_ActivityNumber",
                table: "TrainingActivities",
                column: "ActivityNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingActivities_CatalogItemId",
                table: "TrainingActivities",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingActivities_ExaminerPersonId",
                table: "TrainingActivities",
                column: "ExaminerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingActivities_InstructorPersonId",
                table: "TrainingActivities",
                column: "InstructorPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingActivities_IsActive",
                table: "TrainingActivities",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingActivities_OrganizationOrgUnitId",
                table: "TrainingActivities",
                column: "OrganizationOrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingActivities_OrgUnitId",
                table: "TrainingActivities",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingActivityResults_EvaluatedByPersonId",
                table: "TrainingActivityResults",
                column: "EvaluatedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingActivityResults_IsActive",
                table: "TrainingActivityResults",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingActivityResults_ParticipationId",
                table: "TrainingActivityResults",
                column: "ParticipationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingCatalogItems_ActivityCode_VersionNumber",
                table: "TrainingCatalogItems",
                columns: new[] { "ActivityCode", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingCatalogItems_IsActive",
                table: "TrainingCatalogItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingParticipations_ActivityId_PersonId",
                table: "TrainingParticipations",
                columns: new[] { "ActivityId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingParticipations_IsActive",
                table: "TrainingParticipations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingParticipations_PersonId",
                table: "TrainingParticipations",
                column: "PersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_PilotTrainings_TrainingParticipations_TrainingParticipation~",
                table: "PilotTrainings",
                column: "TrainingParticipationId",
                principalTable: "TrainingParticipations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PilotTrainings_TrainingParticipations_TrainingParticipation~",
                table: "PilotTrainings");

            migrationBuilder.DropTable(
                name: "CertificateScopes");

            migrationBuilder.DropTable(
                name: "PersonFunctionAssignments");

            migrationBuilder.DropTable(
                name: "PilotRestrictions");

            migrationBuilder.DropTable(
                name: "QualificationDocumentVerifications");

            migrationBuilder.DropTable(
                name: "TrainingActivityResults");

            migrationBuilder.DropTable(
                name: "TrainingParticipations");

            migrationBuilder.DropTable(
                name: "TrainingActivities");

            migrationBuilder.DropTable(
                name: "TrainingCatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_PilotTrainings_TrainingParticipationId",
                table: "PilotTrainings");

            migrationBuilder.DropColumn(
                name: "TrainingParticipationId",
                table: "PilotTrainings");
        }
    }
}
