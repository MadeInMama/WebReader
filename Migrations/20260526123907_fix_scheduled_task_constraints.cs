using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebReader.Migrations
{
    /// <inheritdoc />
    public partial class fix_scheduled_task_constraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledTasks_ScheduledTaskConfigs_ScheduledTaskConfigId",
                table: "ScheduledTasks");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledTasks_ScheduledTaskConfigs_ScheduledTaskConfigId",
                table: "ScheduledTasks");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScheduledTaskConfigId",
                table: "ScheduledTasks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledTasks_ScheduledTaskConfigs_ScheduledTaskConfigId",
                table: "ScheduledTasks",
                column: "ScheduledTaskConfigId",
                principalTable: "ScheduledTaskConfigs",
                principalColumn: "Id");
        }
    }
}
