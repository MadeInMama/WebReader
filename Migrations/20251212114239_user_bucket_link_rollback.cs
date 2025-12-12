using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebReader.Migrations
{
    /// <inheritdoc />
    public partial class user_bucket_link_rollback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Buckets_BucketId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_BucketId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BucketId",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Buckets_UserId",
                table: "Buckets",
                column: "UserId",
                unique: true);

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

            migrationBuilder.AddColumn<Guid>(
                name: "BucketId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_BucketId",
                table: "Users",
                column: "BucketId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Buckets_BucketId",
                table: "Users",
                column: "BucketId",
                principalTable: "Buckets",
                principalColumn: "Id");
        }
    }
}
