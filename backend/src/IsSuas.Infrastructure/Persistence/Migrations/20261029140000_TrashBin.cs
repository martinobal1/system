using System;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <summary>Kôš: tabuľka TrashEntries (zmazané záznamy, obnovenie, trvalé vymazanie).</summary>
    [DbContext(typeof(IsSuasDbContext))]
    [Migration("20261029140000_TrashBin")]
    public partial class TrashBin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrashEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OrgUnitPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedByUserName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RestoredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RestoredByUserName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PurgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PurgedByUserName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrashEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrashEntries_Category",
                table: "TrashEntries",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_TrashEntries_EntityType_EntityId",
                table: "TrashEntries",
                columns: new[] { "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrashEntries");
        }
    }
}
