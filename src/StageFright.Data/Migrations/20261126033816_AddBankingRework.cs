using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBankingRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankDeposits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CashAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ChequeTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReversalJournalEntryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankDeposits_Accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDeposits_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDeposits_JournalEntries_ReversalJournalEntryId",
                        column: x => x.ReversalJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceivedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "TEXT", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PaymentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedItems", x => x.Id);
                    table.CheckConstraint("CK_ReceivedItems_ExactlyOneSource", "([PaymentId] IS NULL) <> ([JournalEntryId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ReceivedItems_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceivedItems_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankDepositLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BankDepositId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReceivedItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankDepositLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankDepositLines_BankDeposits_BankDepositId",
                        column: x => x.BankDepositId,
                        principalTable: "BankDeposits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankDepositLines_ReceivedItems_ReceivedItemId",
                        column: x => x.ReceivedItemId,
                        principalTable: "ReceivedItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // An organisation's own "Petty Cash" account would clash with the renamed system account
            // (names are unique by AccountService rule), so it becomes "Petty Cash (own)" first.
            migrationBuilder.Sql("""
                UPDATE Accounts SET Name = 'Petty Cash (own)', UpdatedAt = strftime('%Y-%m-%d %H:%M:%S', 'now')
                WHERE IsSystem = 0 AND Name = 'Petty Cash' COLLATE NOCASE;
                """);

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Name",
                value: "Petty Cash");

            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "Id", "AccountNumber", "CreatedAt", "DeletedAt", "DeletedBy", "IsDeleted", "IsSystem", "Name", "SortOrder", "Type", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), "1105", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, false, true, "Undeposited Funds", 2, "Asset", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_BankDepositLines_BankDepositId_ReceivedItemId",
                table: "BankDepositLines",
                columns: new[] { "BankDepositId", "ReceivedItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankDepositLines_ReceivedItemId",
                table: "BankDepositLines",
                column: "ReceivedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDeposits_BankAccountId",
                table: "BankDeposits",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDeposits_JournalEntryId",
                table: "BankDeposits",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankDeposits_ReversalJournalEntryId",
                table: "BankDeposits",
                column: "ReversalJournalEntryId",
                unique: true,
                filter: "[ReversalJournalEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedItems_JournalEntryId",
                table: "ReceivedItems",
                column: "JournalEntryId",
                unique: true,
                filter: "[JournalEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedItems_PaymentId",
                table: "ReceivedItems",
                column: "PaymentId",
                unique: true,
                filter: "[PaymentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankDepositLines");

            migrationBuilder.DropTable(
                name: "BankDeposits");

            migrationBuilder.DropTable(
                name: "ReceivedItems");

            migrationBuilder.DeleteData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"));

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Name",
                value: "Cash on Hand");

            migrationBuilder.Sql("""
                UPDATE Accounts SET Name = 'Petty Cash', UpdatedAt = strftime('%Y-%m-%d %H:%M:%S', 'now')
                WHERE IsSystem = 0 AND Name = 'Petty Cash (own)';
                """);
        }
    }
}
