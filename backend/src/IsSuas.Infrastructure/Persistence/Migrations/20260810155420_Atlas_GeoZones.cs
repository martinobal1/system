using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Atlas_GeoZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeoZoneImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DataVersionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ImportedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResultMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StoredFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeatureCollectionGeoJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeoZoneImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeoZoneImports_Accounts_ImportedByAccountId",
                        column: x => x.ImportedByAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeoZoneImports_StoredFiles_StoredFileId",
                        column: x => x.StoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GeoZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ZoneType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RestrictionDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FlightConditions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HorizontalLimits = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VerticalLimits = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AuthorityContact = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SourceLabel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DescriptionHtml = table.Column<string>(type: "text", nullable: true),
                    GeometryGeoJson = table.Column<string>(type: "text", nullable: false),
                    PropertiesJson = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_GeoZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeoZones_GeoZoneImports_ImportId",
                        column: x => x.ImportId,
                        principalTable: "GeoZoneImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeoZoneImports_ContentHash",
                table: "GeoZoneImports",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_GeoZoneImports_ImportedAt",
                table: "GeoZoneImports",
                column: "ImportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GeoZoneImports_ImportedByAccountId",
                table: "GeoZoneImports",
                column: "ImportedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_GeoZoneImports_IsActive",
                table: "GeoZoneImports",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GeoZoneImports_Status",
                table: "GeoZoneImports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GeoZoneImports_StoredFileId",
                table: "GeoZoneImports",
                column: "StoredFileId");

            migrationBuilder.CreateIndex(
                name: "IX_GeoZones_ImportId_Name",
                table: "GeoZones",
                columns: new[] { "ImportId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_GeoZones_ImportId_Sequence",
                table: "GeoZones",
                columns: new[] { "ImportId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_GeoZones_IsActive",
                table: "GeoZones",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeoZones");

            migrationBuilder.DropTable(
                name: "GeoZoneImports");
        }
    }
}
