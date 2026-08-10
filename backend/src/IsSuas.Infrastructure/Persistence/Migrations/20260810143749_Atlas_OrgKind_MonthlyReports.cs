using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Atlas_OrgKind_MonthlyReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "OrgUnits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MonthlyFlightReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyFlightReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlyFlightReports_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PilotUasAuthorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PilotId = table.Column<Guid>(type: "uuid", nullable: false),
                    UasId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotUasAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PilotUasAuthorizations_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PilotUasAuthorizations_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PilotUasAuthorizations_UasAircraft_UasId",
                        column: x => x.UasId,
                        principalTable: "UasAircraft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyFlightReportChangeLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviousJson = table.Column<string>(type: "text", nullable: true),
                    NewJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyFlightReportChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlyFlightReportChangeLogs_MonthlyFlightReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "MonthlyFlightReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyFlightReportLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    PilotId = table.Column<Guid>(type: "uuid", nullable: false),
                    UasId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hours = table.Column<int>(type: "integer", nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    Takeoffs = table.Column<int>(type: "integer", nullable: false),
                    Landings = table.Column<int>(type: "integer", nullable: false),
                    OperationalFlights = table.Column<int>(type: "integer", nullable: false),
                    TrainingFlights = table.Column<int>(type: "integer", nullable: false),
                    OtherFlights = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyFlightReportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlyFlightReportLines_MonthlyFlightReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "MonthlyFlightReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MonthlyFlightReportLines_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthlyFlightReportLines_UasAircraft_UasId",
                        column: x => x.UasId,
                        principalTable: "UasAircraft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_Kind",
                table: "OrgUnits",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFlightReportChangeLogs_IsActive",
                table: "MonthlyFlightReportChangeLogs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFlightReportChangeLogs_ReportId_ChangedAt",
                table: "MonthlyFlightReportChangeLogs",
                columns: new[] { "ReportId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFlightReportLines_IsActive",
                table: "MonthlyFlightReportLines",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFlightReportLines_PilotId",
                table: "MonthlyFlightReportLines",
                column: "PilotId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFlightReportLines_ReportId_PilotId_UasId",
                table: "MonthlyFlightReportLines",
                columns: new[] { "ReportId", "PilotId", "UasId" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFlightReportLines_UasId",
                table: "MonthlyFlightReportLines",
                column: "UasId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFlightReports_IsActive",
                table: "MonthlyFlightReports",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFlightReports_OrgUnitId_Year_Month",
                table: "MonthlyFlightReports",
                columns: new[] { "OrgUnitId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyFlightReports_Year_Month_Status",
                table: "MonthlyFlightReports",
                columns: new[] { "Year", "Month", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotUasAuthorizations_IsActive",
                table: "PilotUasAuthorizations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PilotUasAuthorizations_OrgUnitId_IsActive",
                table: "PilotUasAuthorizations",
                columns: new[] { "OrgUnitId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotUasAuthorizations_PilotId_UasId_ValidFrom",
                table: "PilotUasAuthorizations",
                columns: new[] { "PilotId", "UasId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotUasAuthorizations_UasId",
                table: "PilotUasAuthorizations",
                column: "UasId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthlyFlightReportChangeLogs");

            migrationBuilder.DropTable(
                name: "MonthlyFlightReportLines");

            migrationBuilder.DropTable(
                name: "PilotUasAuthorizations");

            migrationBuilder.DropTable(
                name: "MonthlyFlightReports");

            migrationBuilder.DropIndex(
                name: "IX_OrgUnits_Kind",
                table: "OrgUnits");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "OrgUnits");
        }
    }
}
