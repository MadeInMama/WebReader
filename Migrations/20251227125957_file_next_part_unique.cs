using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebReader.Migrations
{
    /// <inheritdoc />
    public partial class file_next_part_unique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Files_NextPartId",
                table: "Files");

            migrationBuilder.CreateIndex(
                name: "IX_Files_NextPartId",
                table: "Files",
                column: "NextPartId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Files_NextPartId",
                table: "Files");

            migrationBuilder.CreateIndex(
                name: "IX_Files_NextPartId",
                table: "Files",
                column: "NextPartId");
        }
    }
}
