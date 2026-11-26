using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalOrganisations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExternalOrganisationId",
                table: "JournalEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalOrganisations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    Address = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalOrganisations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalOrganisationContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalOrganisationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Mobile = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalOrganisationContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalOrganisationContacts_ExternalOrganisations_ExternalOrganisationId",
                        column: x => x.ExternalOrganisationId,
                        principalTable: "ExternalOrganisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ExternalOrganisationId",
                table: "JournalEntries",
                column: "ExternalOrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrganisationContacts_ExternalOrganisationId",
                table: "ExternalOrganisationContacts",
                column: "ExternalOrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrganisations_Name",
                table: "ExternalOrganisations",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_ExternalOrganisations_ExternalOrganisationId",
                table: "JournalEntries",
                column: "ExternalOrganisationId",
                principalTable: "ExternalOrganisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_ExternalOrganisations_ExternalOrganisationId",
                table: "JournalEntries");

            migrationBuilder.DropTable(
                name: "ExternalOrganisationContacts");

            migrationBuilder.DropTable(
                name: "ExternalOrganisations");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_ExternalOrganisationId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ExternalOrganisationId",
                table: "JournalEntries");
        }
    }
}
