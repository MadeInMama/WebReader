using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebReader.Migrations
{
    /// <inheritdoc />
    public partial class tasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.CreateTable(
                name: "ScheduledTaskConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    DefaultPriority = table.Column<short>(type: "smallint", nullable: false),
                    Cron = table.Column<int>(type: "integer", nullable: false),
                    Settings = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTaskConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ScheduledTaskConfigId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledTasks_ScheduledTaskConfigs_ScheduledTaskConfigId",
                        column: x => x.ScheduledTaskConfigId,
                        principalTable: "ScheduledTaskConfigs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTasks_Priority",
                table: "ScheduledTasks",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTasks_ScheduledTaskConfigId",
                table: "ScheduledTasks",
                column: "ScheduledTaskConfigId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledTasks");

            migrationBuilder.DropTable(
                name: "ScheduledTaskConfigs");

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });
        }
    }
}
