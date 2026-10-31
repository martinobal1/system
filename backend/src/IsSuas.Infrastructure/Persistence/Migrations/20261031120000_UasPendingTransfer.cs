using System;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <summary>Ponuka UAS útvaru: gestor ponúkne, správca útvaru prijme alebo odmietne.</summary>
    [DbContext(typeof(IsSuasDbContext))]
    [Migration("20261031120000_UasPendingTransfer")]
    public partial class UasPendingTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PendingOrgUnitId",
                table: "UasAircraft",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PendingSince",
                table: "UasAircraft",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingReason",
                table: "UasAircraft",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UasAircraft_PendingOrgUnitId",
                table: "UasAircraft",
                column: "PendingOrgUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UasAircraft_PendingOrgUnitId",
                table: "UasAircraft");

            migrationBuilder.DropColumn(name: "PendingOrgUnitId", table: "UasAircraft");
            migrationBuilder.DropColumn(name: "PendingSince", table: "UasAircraft");
            migrationBuilder.DropColumn(name: "PendingReason", table: "UasAircraft");
        }
    }
}
