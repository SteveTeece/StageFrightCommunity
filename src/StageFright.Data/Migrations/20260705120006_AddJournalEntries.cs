using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_JournalEntryId",
                table: "Transactions",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_Type",
                table: "JournalEntries",
                column: "Type");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_JournalEntries_JournalEntryId",
                table: "Transactions",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_JournalEntries_JournalEntryId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_JournalEntryId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "Transactions");
        }
    }
}
