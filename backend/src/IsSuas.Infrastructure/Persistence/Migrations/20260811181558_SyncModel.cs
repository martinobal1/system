using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CertificateNumber",
                table: "UasAircraft",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeregisteredAt",
                table: "UasAircraft",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InventoryAssetNumber",
                table: "UasAircraft",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogbookNumber",
                table: "UasAircraft",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GestorCertificateNumber",
                table: "Pilots",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogbookNumber",
                table: "Pilots",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalBadgeNumber",
                table: "Pilots",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MtowOrValidityText",
                table: "PilotCertificates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UasAircraft_CertificateNumber",
                table: "UasAircraft",
                column: "CertificateNumber");

            migrationBuilder.CreateIndex(
                name: "IX_UasAircraft_InventoryAssetNumber",
                table: "UasAircraft",
                column: "InventoryAssetNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UasAircraft_CertificateNumber",
                table: "UasAircraft");

            migrationBuilder.DropIndex(
                name: "IX_UasAircraft_InventoryAssetNumber",
                table: "UasAircraft");

            migrationBuilder.DropColumn(
                name: "CertificateNumber",
                table: "UasAircraft");

            migrationBuilder.DropColumn(
                name: "DeregisteredAt",
                table: "UasAircraft");

            migrationBuilder.DropColumn(
                name: "InventoryAssetNumber",
                table: "UasAircraft");

            migrationBuilder.DropColumn(
                name: "LogbookNumber",
                table: "UasAircraft");

            migrationBuilder.DropColumn(
                name: "GestorCertificateNumber",
                table: "Pilots");

            migrationBuilder.DropColumn(
                name: "LogbookNumber",
                table: "Pilots");

            migrationBuilder.DropColumn(
                name: "PersonalBadgeNumber",
                table: "Pilots");

            migrationBuilder.DropColumn(
                name: "MtowOrValidityText",
                table: "PilotCertificates");
        }
    }
}
