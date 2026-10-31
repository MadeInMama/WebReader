using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebReader.Migrations
{
    /// <inheritdoc />
    public partial class subscriber_pwa_multiple : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubscriberPwas_UserId",
                table: "SubscriberPwas");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberPwas_UserId",
                table: "SubscriberPwas",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubscriberPwas_UserId",
                table: "SubscriberPwas");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberPwas_UserId",
                table: "SubscriberPwas",
                column: "UserId",
                unique: true);
        }
    }
}
