using System;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <summary>Nadriadený prevádzkovateľ pilota (každý pilot má najviac jedného).</summary>
    [DbContext(typeof(IsSuasDbContext))]
    [Migration("20261102120000_PilotOperator")]
    public partial class PilotOperator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperatorAccountId",
                table: "Pilots",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pilots_OperatorAccountId",
                table: "Pilots",
                column: "OperatorAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pilots_Accounts_OperatorAccountId",
                table: "Pilots",
                column: "OperatorAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Pilots_Accounts_OperatorAccountId", table: "Pilots");
            migrationBuilder.DropIndex(name: "IX_Pilots_OperatorAccountId", table: "Pilots");
            migrationBuilder.DropColumn(name: "OperatorAccountId", table: "Pilots");
        }
    }
}
