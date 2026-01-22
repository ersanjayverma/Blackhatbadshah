using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserWorkerConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserWorkerConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    PskHash = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    ConfigName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    MaxWorkers = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastPskRotatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastWorkerActivityAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkerConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkerConfigs_UserId",
                table: "UserWorkerConfigs",
                column: "UserId",
                unique: true);

            // Update WorkerAgents indexes - drop old workspace-based indexes and create user-based ones
            migrationBuilder.DropIndex(
                name: "IX_WorkerAgents_WorkspaceId_Name",
                table: "WorkerAgents");

            migrationBuilder.DropIndex(
                name: "IX_WorkerAgents_WorkspaceId_Status",
                table: "WorkerAgents");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerAgents_CreatedByUserId_Name",
                table: "WorkerAgents",
                columns: new[] { "CreatedByUserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerAgents_CreatedByUserId_Status",
                table: "WorkerAgents",
                columns: new[] { "CreatedByUserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserWorkerConfigs");

            // Restore old indexes
            migrationBuilder.DropIndex(
                name: "IX_WorkerAgents_CreatedByUserId_Name",
                table: "WorkerAgents");

            migrationBuilder.DropIndex(
                name: "IX_WorkerAgents_CreatedByUserId_Status",
                table: "WorkerAgents");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerAgents_WorkspaceId_Name",
                table: "WorkerAgents",
                columns: new[] { "WorkspaceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerAgents_WorkspaceId_Status",
                table: "WorkerAgents",
                columns: new[] { "WorkspaceId", "Status" });
        }
    }
}
