using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Atlas_Wave3_DocumentStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RetentionRuleId",
                table: "ManagedDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoredFileId",
                table: "ManagedDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentDossiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DescriptionSk = table.Column<string>(type: "text", nullable: true),
                    LinkedEntityType = table.Column<int>(type: "integer", nullable: true),
                    LinkedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentDossiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetentionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NameSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    ArchiveMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DescriptionSk = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoredFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScanStatus = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentDossierItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentDossierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagedDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentDossierItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentDossierItems_DocumentDossiers_DocumentDossierId",
                        column: x => x.DocumentDossierId,
                        principalTable: "DocumentDossiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentDossierItems_ManagedDocuments_ManagedDocumentId",
                        column: x => x.ManagedDocumentId,
                        principalTable: "ManagedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocuments_RetentionRuleId",
                table: "ManagedDocuments",
                column: "RetentionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocuments_StoredFileId",
                table: "ManagedDocuments",
                column: "StoredFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDossierItems_DocumentDossierId_ManagedDocumentId",
                table: "DocumentDossierItems",
                columns: new[] { "DocumentDossierId", "ManagedDocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDossierItems_IsActive",
                table: "DocumentDossierItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDossierItems_ManagedDocumentId",
                table: "DocumentDossierItems",
                column: "ManagedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDossiers_IsActive",
                table: "DocumentDossiers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionRules_Code",
                table: "RetentionRules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetentionRules_IsActive",
                table: "RetentionRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_ContentHash",
                table: "StoredFiles",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_IsActive",
                table: "StoredFiles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_StorageKey",
                table: "StoredFiles",
                column: "StorageKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ManagedDocuments_RetentionRules_RetentionRuleId",
                table: "ManagedDocuments",
                column: "RetentionRuleId",
                principalTable: "RetentionRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ManagedDocuments_StoredFiles_StoredFileId",
                table: "ManagedDocuments",
                column: "StoredFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ManagedDocuments_RetentionRules_RetentionRuleId",
                table: "ManagedDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ManagedDocuments_StoredFiles_StoredFileId",
                table: "ManagedDocuments");

            migrationBuilder.DropTable(
                name: "DocumentDossierItems");

            migrationBuilder.DropTable(
                name: "RetentionRules");

            migrationBuilder.DropTable(
                name: "StoredFiles");

            migrationBuilder.DropTable(
                name: "DocumentDossiers");

            migrationBuilder.DropIndex(
                name: "IX_ManagedDocuments_RetentionRuleId",
                table: "ManagedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ManagedDocuments_StoredFileId",
                table: "ManagedDocuments");

            migrationBuilder.DropColumn(
                name: "RetentionRuleId",
                table: "ManagedDocuments");

            migrationBuilder.DropColumn(
                name: "StoredFileId",
                table: "ManagedDocuments");
        }
    }
}
