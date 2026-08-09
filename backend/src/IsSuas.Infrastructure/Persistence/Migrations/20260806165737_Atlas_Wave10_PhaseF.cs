using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Atlas_Wave10_PhaseF : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationMapChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsBlocking = table.Column<bool>(type: "boolean", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CheckedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestGeoJson = table.Column<string>(type: "text", nullable: true),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationMapChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationMapChecks_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationMapChecks_IsActive",
                table: "OperationMapChecks",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OperationMapChecks_OperationId_CheckedAt",
                table: "OperationMapChecks",
                columns: new[] { "OperationId", "CheckedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationMapChecks");
        }
    }
}
