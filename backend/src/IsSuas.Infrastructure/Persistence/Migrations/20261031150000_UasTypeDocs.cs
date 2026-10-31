using System;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <summary>Technická dokumentácia typov UAS v databáze (namiesto prehliadača) + PDF manuál výrobcu.</summary>
    [DbContext(typeof(IsSuasDbContext))]
    [Migration("20261031150000_UasTypeDocs")]
    public partial class UasTypeDocs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UasTypeDocs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Manufacturer = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    ManualStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ManualFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    ManualSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ManualUploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UasTypeDocs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UasTypeDocs_IsActive",
                table: "UasTypeDocs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UasTypeDocs_Slug",
                table: "UasTypeDocs",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UasTypeDocs");
        }
    }
}
