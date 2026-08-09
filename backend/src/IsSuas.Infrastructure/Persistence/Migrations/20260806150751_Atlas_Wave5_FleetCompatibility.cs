using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Atlas_Wave5_FleetCompatibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BatteryCompatibilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatteryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UasId = table.Column<Guid>(type: "uuid", nullable: true),
                    UasTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RestrictionText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    VerificationDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryCompatibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatteryCompatibilities_Batteries_BatteryId",
                        column: x => x.BatteryId,
                        principalTable: "Batteries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BatteryCompatibilities_UasAircraft_UasId",
                        column: x => x.UasId,
                        principalTable: "UasAircraft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BatteryOperationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatteryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PerformedByPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedFlightId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChargeLevelPercent = table.Column<int>(type: "integer", nullable: true),
                    CycleCount = table.Column<int>(type: "integer", nullable: true),
                    TemperatureC = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ResultCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryOperationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatteryOperationRecords_Batteries_BatteryId",
                        column: x => x.BatteryId,
                        principalTable: "Batteries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BatteryOperationRecords_Flights_RelatedFlightId",
                        column: x => x.RelatedFlightId,
                        principalTable: "Flights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BatteryOperationRecords_Persons_PerformedByPersonId",
                        column: x => x.PerformedByPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BatteryStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatteryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusKind = table.Column<int>(type: "integer", nullable: false),
                    PreviousStatus = table.Column<int>(type: "integer", nullable: true),
                    NewStatus = table.Column<int>(type: "integer", nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ChangedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatteryStatusHistories_Batteries_BatteryId",
                        column: x => x.BatteryId,
                        principalTable: "Batteries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BatteryTechnicalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatteryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MetricType = table.Column<int>(type: "integer", nullable: false),
                    MeasuredValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    UnitCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TextValue = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    SourceFlightId = table.Column<Guid>(type: "uuid", nullable: true),
                    MeasuredByPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryTechnicalRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatteryTechnicalRecords_Batteries_BatteryId",
                        column: x => x.BatteryId,
                        principalTable: "Batteries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BatteryTechnicalRecords_Flights_SourceFlightId",
                        column: x => x.SourceFlightId,
                        principalTable: "Flights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BatteryTechnicalRecords_Persons_MeasuredByPersonId",
                        column: x => x.MeasuredByPersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ComponentCompatibilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UasId = table.Column<Guid>(type: "uuid", nullable: true),
                    UasTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RestrictionText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    VerificationDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentCompatibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComponentCompatibilities_Components_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "Components",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComponentCompatibilities_UasAircraft_UasId",
                        column: x => x.UasId,
                        principalTable: "UasAircraft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatteryCompatibilities_BatteryId_UasId_UasTypeCode_ValidFrom",
                table: "BatteryCompatibilities",
                columns: new[] { "BatteryId", "UasId", "UasTypeCode", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_BatteryCompatibilities_IsActive",
                table: "BatteryCompatibilities",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryCompatibilities_UasId",
                table: "BatteryCompatibilities",
                column: "UasId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryOperationRecords_BatteryId_OccurredAt",
                table: "BatteryOperationRecords",
                columns: new[] { "BatteryId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BatteryOperationRecords_IsActive",
                table: "BatteryOperationRecords",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryOperationRecords_PerformedByPersonId",
                table: "BatteryOperationRecords",
                column: "PerformedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryOperationRecords_RelatedFlightId",
                table: "BatteryOperationRecords",
                column: "RelatedFlightId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryStatusHistories_BatteryId_EffectiveAt",
                table: "BatteryStatusHistories",
                columns: new[] { "BatteryId", "EffectiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BatteryStatusHistories_IsActive",
                table: "BatteryStatusHistories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryTechnicalRecords_BatteryId_RecordedAt",
                table: "BatteryTechnicalRecords",
                columns: new[] { "BatteryId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BatteryTechnicalRecords_IsActive",
                table: "BatteryTechnicalRecords",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryTechnicalRecords_MeasuredByPersonId",
                table: "BatteryTechnicalRecords",
                column: "MeasuredByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryTechnicalRecords_SourceFlightId",
                table: "BatteryTechnicalRecords",
                column: "SourceFlightId");

            migrationBuilder.CreateIndex(
                name: "IX_ComponentCompatibilities_ComponentId_UasId_UasTypeCode_Vali~",
                table: "ComponentCompatibilities",
                columns: new[] { "ComponentId", "UasId", "UasTypeCode", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ComponentCompatibilities_IsActive",
                table: "ComponentCompatibilities",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ComponentCompatibilities_UasId",
                table: "ComponentCompatibilities",
                column: "UasId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatteryCompatibilities");

            migrationBuilder.DropTable(
                name: "BatteryOperationRecords");

            migrationBuilder.DropTable(
                name: "BatteryStatusHistories");

            migrationBuilder.DropTable(
                name: "BatteryTechnicalRecords");

            migrationBuilder.DropTable(
                name: "ComponentCompatibilities");
        }
    }
}
