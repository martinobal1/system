using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsSuas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Atlas_Wave4_Notifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EscalationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NameSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Trigger = table.Column<int>(type: "integer", nullable: false),
                    ThresholdHours = table.Column<int>(type: "integer", nullable: false),
                    TargetRoleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EscalationMessageTemplate = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NameSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SubjectTemplate = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    BodyTemplate = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppNotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationRecipients_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NotificationRecipients_AppNotifications_AppNotificationId",
                        column: x => x.AppNotificationId,
                        principalTable: "AppNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkTaskHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    ChangedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTaskHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTaskHistories_WorkTasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppNotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationRecipientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryAttempts_AppNotifications_AppNotificationId",
                        column: x => x.AppNotificationId,
                        principalTable: "AppNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryAttempts_NotificationRecipients_NotificationRecipie~",
                        column: x => x.NotificationRecipientId,
                        principalTable: "NotificationRecipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAttempts_AppNotificationId_AttemptNumber",
                table: "DeliveryAttempts",
                columns: new[] { "AppNotificationId", "AttemptNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAttempts_IsActive",
                table: "DeliveryAttempts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAttempts_NotificationRecipientId",
                table: "DeliveryAttempts",
                column: "NotificationRecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_Code",
                table: "EscalationRules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_IsActive",
                table: "EscalationRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplates_Code",
                table: "MessageTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageTemplates_IsActive",
                table: "MessageTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_AccountId",
                table: "NotificationRecipients",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_AppNotificationId_AccountId_RoleCode",
                table: "NotificationRecipients",
                columns: new[] { "AppNotificationId", "AccountId", "RoleCode" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_IsActive",
                table: "NotificationRecipients",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskHistories_IsActive",
                table: "WorkTaskHistories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskHistories_WorkTaskId_ChangedAt",
                table: "WorkTaskHistories",
                columns: new[] { "WorkTaskId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryAttempts");

            migrationBuilder.DropTable(
                name: "EscalationRules");

            migrationBuilder.DropTable(
                name: "MessageTemplates");

            migrationBuilder.DropTable(
                name: "WorkTaskHistories");

            migrationBuilder.DropTable(
                name: "NotificationRecipients");
        }
    }
}
