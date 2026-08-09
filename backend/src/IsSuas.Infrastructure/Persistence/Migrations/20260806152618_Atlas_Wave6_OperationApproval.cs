using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Atlas_Wave6_OperationApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecurrenceSourceOperationId",
                table: "Operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OperationApprovalSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    StepTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssignedAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedRoleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationApprovalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationApprovalSteps_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationRecurrenceRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecurrenceType = table.Column<int>(type: "integer", nullable: false),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    DaysOfWeek = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MaximumOccurrences = table.Column<int>(type: "integer", nullable: true),
                    NextOccurrenceAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GeneratedCount = table.Column<int>(type: "integer", nullable: false),
                    IsRuleActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationRecurrenceRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationRecurrenceRules_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionCode = table.Column<int>(type: "integer", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Justification = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationDecisions_OperationApprovalSteps_ApprovalStepId",
                        column: x => x.ApprovalStepId,
                        principalTable: "OperationApprovalSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationApprovalConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConditionTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MustBeMetBeforeFlight = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerifiedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerificationNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationApprovalConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationApprovalConditions_OperationDecisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "OperationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationApprovalConditions_DecisionId_Status",
                table: "OperationApprovalConditions",
                columns: new[] { "DecisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationApprovalConditions_IsActive",
                table: "OperationApprovalConditions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OperationApprovalSteps_IsActive",
                table: "OperationApprovalSteps",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OperationApprovalSteps_OperationId_SequenceNumber",
                table: "OperationApprovalSteps",
                columns: new[] { "OperationId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationDecisions_ApprovalStepId",
                table: "OperationDecisions",
                column: "ApprovalStepId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationDecisions_IsActive",
                table: "OperationDecisions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OperationRecurrenceRules_IsActive",
                table: "OperationRecurrenceRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OperationRecurrenceRules_OperationId",
                table: "OperationRecurrenceRules",
                column: "OperationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationApprovalConditions");

            migrationBuilder.DropTable(
                name: "OperationRecurrenceRules");

            migrationBuilder.DropTable(
                name: "OperationDecisions");

            migrationBuilder.DropTable(
                name: "OperationApprovalSteps");

            migrationBuilder.DropColumn(
                name: "RecurrenceSourceOperationId",
                table: "Operations");
        }
    }
}
