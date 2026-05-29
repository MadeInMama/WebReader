using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebReader.Migrations
{
    /// <inheritdoc />
    public partial class add_settings_to_scheduled_task : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledTasks_ScheduledTaskConfigs_ScheduledTaskConfigId",
                table: "ScheduledTasks");

            migrationBuilder.RenameColumn(
                name: "Settings",
                table: "ScheduledTaskConfigs",
                newName: "DefaultSettings");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScheduledTaskConfigId",
                table: "ScheduledTasks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "Settings",
                table: "ScheduledTasks",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledTasks_ScheduledTaskConfigs_ScheduledTaskConfigId",
                table: "ScheduledTasks",
                column: "ScheduledTaskConfigId",
                principalTable: "ScheduledTaskConfigs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledTasks_ScheduledTaskConfigs_ScheduledTaskConfigId",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "Settings",
                table: "ScheduledTasks");

            migrationBuilder.RenameColumn(
                name: "DefaultSettings",
                table: "ScheduledTaskConfigs",
                newName: "Settings");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScheduledTaskConfigId",
                table: "ScheduledTasks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledTasks_ScheduledTaskConfigs_ScheduledTaskConfigId",
                table: "ScheduledTasks",
                column: "ScheduledTaskConfigId",
                principalTable: "ScheduledTaskConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
