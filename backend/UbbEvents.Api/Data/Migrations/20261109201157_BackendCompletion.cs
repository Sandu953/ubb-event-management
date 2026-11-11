using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UbbEvents.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackendCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve existing spelling/history while enforcing normalized-email uniqueness.
            // If legacy data has case/whitespace duplicates, fail for human resolution rather than deleting users.
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_Users_NormalizedEmail\" ON \"Users\" (lower(btrim(\"Email\")))");
            // Initial infrastructure created events with Version=0; API update contracts start at 1.
            migrationBuilder.Sql("UPDATE \"Events\" SET \"Version\" = 1 WHERE \"Version\" = 0");
            migrationBuilder.AlterColumn<bool>(
                name: "CheckedIn",
                table: "Registrations",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_GlobalRole",
                table: "Users",
                sql: "\"GlobalRole\" IN ('ADMIN', 'STUDENT')");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_EventId_Status_RegisteredAt_Id",
                table: "Registrations",
                columns: new[] { "EventId", "Status", "RegisteredAt", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Registrations_CheckIn",
                table: "Registrations",
                sql: "NOT \"CheckedIn\" OR \"Status\" = 'CONFIRMED'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Registrations_Status",
                table: "Registrations",
                sql: "\"Status\" IN ('CONFIRMED', 'WAITLISTED', 'CANCELED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrganizationMembers_Role",
                table: "OrganizationMembers",
                sql: "\"OrgRole\" IN ('ORG_ADMIN', 'ORGANIZER')");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Status_StartTime",
                table: "Events",
                columns: new[] { "Status", "StartTime" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Events_Status",
                table: "Events",
                sql: "\"Status\" IN ('DRAFT', 'PUBLISHED', 'REGISTRATION_OPEN', 'COMPLETED', 'CANCELLED')");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId_OccurredAt",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OrganizationId_OccurredAt",
                table: "AuditLogs",
                columns: new[] { "OrganizationId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX \"IX_Users_NormalizedEmail\"");
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_GlobalRole",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_EventId_Status_RegisteredAt_Id",
                table: "Registrations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Registrations_CheckIn",
                table: "Registrations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Registrations_Status",
                table: "Registrations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrganizationMembers_Role",
                table: "OrganizationMembers");

            migrationBuilder.DropIndex(
                name: "IX_Events_Status_StartTime",
                table: "Events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Events_Status",
                table: "Events");

            migrationBuilder.AlterColumn<bool>(
                name: "CheckedIn",
                table: "Registrations",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
