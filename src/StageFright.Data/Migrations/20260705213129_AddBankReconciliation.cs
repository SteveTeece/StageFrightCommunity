using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBankReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StatementDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StatementClosingBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    FinalisedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankReconciliations_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationLines_BankReconciliations_ReconciliationId",
                        column: x => x.ReconciliationId,
                        principalTable: "BankReconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReconciliationLines_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliations_AccountId",
                table: "BankReconciliations",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationLines_ReconciliationId_TransactionId",
                table: "ReconciliationLines",
                columns: new[] { "ReconciliationId", "TransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationLines_TransactionId",
                table: "ReconciliationLines",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReconciliationLines");

            migrationBuilder.DropTable(
                name: "BankReconciliations");
        }
    }
}
