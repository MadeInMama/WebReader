using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebReader.Migrations
{
    /// <inheritdoc />
    public partial class file_next_part : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NextPartId",
                table: "Files",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_NextPartId",
                table: "Files",
                column: "NextPartId");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Files_NextPartId",
                table: "Files",
                column: "NextPartId",
                principalTable: "Files",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Files_NextPartId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_NextPartId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "NextPartId",
                table: "Files");
        }
    }
}
