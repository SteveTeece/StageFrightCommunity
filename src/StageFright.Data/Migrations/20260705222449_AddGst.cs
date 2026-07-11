using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGst : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GstCode",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnualFeeGstCode",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttendanceFeeGstCode",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGstRegistered",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GstCode",
                table: "Fees",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GstCode",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AnnualFeeGstCode",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "AttendanceFeeGstCode",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IsGstRegistered",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "GstCode",
                table: "Fees");
        }
    }
}
