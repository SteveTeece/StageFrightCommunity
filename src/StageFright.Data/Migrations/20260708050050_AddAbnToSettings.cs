using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAbnToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Abn",
                table: "Settings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Abn",
                table: "Settings");
        }
    }
}
