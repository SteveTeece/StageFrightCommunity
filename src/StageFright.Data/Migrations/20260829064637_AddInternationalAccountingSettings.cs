using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInternationalAccountingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "AuditRetentionYears",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 5,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedThroughDate",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Settings",
                type: "TEXT",
                maxLength: 3,
                nullable: false,
                defaultValue: "AUD");

            migrationBuilder.AddColumn<int>(
                name: "FinancialYearStartDay",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedThroughDate",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "FinancialYearStartDay",
                table: "Settings");

            migrationBuilder.AlterColumn<int>(
                name: "AuditRetentionYears",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 5);
        }
    }
}
