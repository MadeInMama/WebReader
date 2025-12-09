using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebReader.Migrations
{
    /// <inheritdoc />
    public partial class personal_buckets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Buckets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Buckets_UserId",
                table: "Buckets",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Buckets_Users_UserId",
                table: "Buckets",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Buckets_Users_UserId",
                table: "Buckets");

            migrationBuilder.DropIndex(
                name: "IX_Buckets_UserId",
                table: "Buckets");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Buckets");
        }
    }
}
