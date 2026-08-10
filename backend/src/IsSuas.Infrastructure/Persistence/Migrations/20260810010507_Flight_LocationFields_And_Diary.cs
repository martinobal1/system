using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Flight_LocationFields_And_Diary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlightNumber",
                table: "Flights",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Flights",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLat",
                table: "Flights",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLon",
                table: "Flights",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationRadiusMeters",
                table: "Flights",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "Flights",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_OrgUnitId",
                table: "SafetyEvents",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_FlightNumber",
                table: "Flights",
                column: "FlightNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_SafetyEvents_OrgUnits_OrgUnitId",
                table: "SafetyEvents",
                column: "OrgUnitId",
                principalTable: "OrgUnits",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SafetyEvents_OrgUnits_OrgUnitId",
                table: "SafetyEvents");

            migrationBuilder.DropIndex(
                name: "IX_SafetyEvents_OrgUnitId",
                table: "SafetyEvents");

            migrationBuilder.DropIndex(
                name: "IX_Flights_FlightNumber",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "FlightNumber",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "LocationLat",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "LocationLon",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "LocationRadiusMeters",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Flights");
        }
    }
}
