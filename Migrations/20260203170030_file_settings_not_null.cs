using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebReader.Migrations
{
    /// <inheritdoc />
    public partial class file_settings_not_null : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<IDictionary<string, string>>(
                name: "Settings",
                table: "Files",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(IDictionary<string, string>),
                oldType: "jsonb",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<IDictionary<string, string>>(
                name: "Settings",
                table: "Files",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(IDictionary<string, string>),
                oldType: "jsonb");
        }
    }
}
