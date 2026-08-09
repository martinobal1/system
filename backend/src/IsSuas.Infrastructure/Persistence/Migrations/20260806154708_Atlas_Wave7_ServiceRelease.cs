using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Atlas_Wave7_ServiceRelease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ServiceRequestId",
                table: "WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReleasesToService",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefectId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    ReleaseScopeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RestrictionDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReleasedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
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
                    table.PrimaryKey("PK_ReleasesToService", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleasesToService_Defects_DefectId",
                        column: x => x.DefectId,
                        principalTable: "Defects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReleasesToService_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DefectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceFlightId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredCompletionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceRequests_Defects_DefectId",
                        column: x => x.DefectId,
                        principalTable: "Defects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalRestrictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefectId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestrictionTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BlocksOperation = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TerminationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalRestrictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicalRestrictions_Defects_DefectId",
                        column: x => x.DefectId,
                        principalTable: "Defects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatusCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NewStatusCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceDefectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceReleaseId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_TechnicalStatusHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ServiceRequestId",
                table: "WorkOrders",
                column: "ServiceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleasesToService_DefectId",
                table: "ReleasesToService",
                column: "DefectId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleasesToService_IsActive",
                table: "ReleasesToService",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ReleasesToService_TargetType_TargetId_EffectiveFrom",
                table: "ReleasesToService",
                columns: new[] { "TargetType", "TargetId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ReleasesToService_WorkOrderId",
                table: "ReleasesToService",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_DefectId",
                table: "ServiceRequests",
                column: "DefectId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_IsActive",
                table: "ServiceRequests",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_RequestNumber",
                table: "ServiceRequests",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_TargetType_TargetId",
                table: "ServiceRequests",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalRestrictions_DefectId",
                table: "TechnicalRestrictions",
                column: "DefectId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalRestrictions_IsActive",
                table: "TechnicalRestrictions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalRestrictions_TargetType_TargetId_Status",
                table: "TechnicalRestrictions",
                columns: new[] { "TargetType", "TargetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalStatusHistories_IsActive",
                table: "TechnicalStatusHistories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalStatusHistories_TargetType_TargetId_EffectiveAt",
                table: "TechnicalStatusHistories",
                columns: new[] { "TargetType", "TargetId", "EffectiveAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_ServiceRequests_ServiceRequestId",
                table: "WorkOrders",
                column: "ServiceRequestId",
                principalTable: "ServiceRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_ServiceRequests_ServiceRequestId",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "ReleasesToService");

            migrationBuilder.DropTable(
                name: "ServiceRequests");

            migrationBuilder.DropTable(
                name: "TechnicalRestrictions");

            migrationBuilder.DropTable(
                name: "TechnicalStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_ServiceRequestId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ServiceRequestId",
                table: "WorkOrders");
        }
    }
}
