using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StageFright.Data.Migrations
{
    /// <summary>
    /// Converts the legacy Categories table into the full chart-of-accounts Accounts table.
    /// Hand-written to be SQLite-safe: renames, adds, and data updates only — no table
    /// rebuilds and ZERO statements that touch Transactions row data (Transaction.GLAccount
    /// strings are posting-time snapshots and stay as posted; aggregation keys on AccountId).
    /// Renumbering: user Income 1000+n → 4000+n, user Expense 2000+n → 6000+n;
    /// system rows become Cash on Hand 1100 (Asset, bank), Member Receivable 1200 (Asset),
    /// Bad Debt Expense 6999. New system accounts: 2310, 2320, 3100, 3200.
    /// The unique index on AccountNumber doubles as the defensive collision assertion.
    /// </summary>
    public partial class ConvertCategoriesToAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Structural renames (all native SQLite ALTERs — no rebuild).
            migrationBuilder.RenameTable(
                name: "Categories",
                newName: "Accounts");

            migrationBuilder.RenameColumn(
                name: "GLAccount",
                table: "Accounts",
                newName: "AccountNumber");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "Transactions",
                newName: "AccountId");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_CategoryId",
                table: "Transactions",
                newName: "IX_Transactions_AccountId");

            // 2. New columns.
            migrationBuilder.AddColumn<bool>(
                name: "IsBankAccount",
                table: "Accounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FinancialYearStartMonth",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 7);

            // 3. System rows take their new identity (fixed GUIDs; data updates only).
            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "Name", "Type", "AccountNumber", "IsBankAccount" },
                values: new object[] { "Cash on Hand", "Asset", "1100", true });

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                columns: new[] { "Type", "AccountNumber" },
                values: new object[] { "Asset", "1200" });

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                columns: new[] { "AccountNumber" },
                values: new object[] { "6999" });

            // 4. Renumber user rows into the AU ranges (Type is stored as a string).
            migrationBuilder.Sql(
                "UPDATE Accounts SET AccountNumber = printf('%04d', CAST(AccountNumber AS INTEGER) + 3000) " +
                "WHERE Type = 'Income' AND IsSystem = 0;");

            migrationBuilder.Sql(
                "UPDATE Accounts SET AccountNumber = printf('%04d', CAST(AccountNumber AS INTEGER) + 4000) " +
                "WHERE Type = 'Expense' AND IsSystem = 0;");

            // 5. New system accounts.
            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "Id", "AccountNumber", "CreatedAt", "DeletedAt", "DeletedBy", "IsBankAccount", "IsDeleted", "IsSystem", "Name", "SortOrder", "Type", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000004"), "2310", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, false, false, true, "GST Collected", 10, "Liability", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000005"), "2320", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, false, false, true, "GST Paid", 11, "Liability", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000006"), "3100", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, false, false, true, "Opening Balance Equity", 20, "Equity", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000007"), "3200", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, false, false, true, "Accumulated Surplus", 21, "Equity", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            // 6. Unique account numbers — also fails fast if the renumbering ever collided.
            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountNumber",
                table: "Accounts",
                column: "AccountNumber",
                unique: true);

            // 7. Schema version bump (minor — backups stay major-version compatible).
            migrationBuilder.Sql("UPDATE Settings SET SchemaVersion = '1.1.0';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Settings SET SchemaVersion = '1.0.0';");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_AccountNumber",
                table: "Accounts");

            migrationBuilder.DeleteData(table: "Accounts", keyColumn: "Id", keyValue: new Guid("00000000-0000-0000-0000-000000000004"));
            migrationBuilder.DeleteData(table: "Accounts", keyColumn: "Id", keyValue: new Guid("00000000-0000-0000-0000-000000000005"));
            migrationBuilder.DeleteData(table: "Accounts", keyColumn: "Id", keyValue: new Guid("00000000-0000-0000-0000-000000000006"));
            migrationBuilder.DeleteData(table: "Accounts", keyColumn: "Id", keyValue: new Guid("00000000-0000-0000-0000-000000000007"));

            migrationBuilder.Sql(
                "UPDATE Accounts SET AccountNumber = printf('%04d', CAST(AccountNumber AS INTEGER) - 3000) " +
                "WHERE Type = 'Income' AND IsSystem = 0;");

            migrationBuilder.Sql(
                "UPDATE Accounts SET AccountNumber = printf('%04d', CAST(AccountNumber AS INTEGER) - 4000) " +
                "WHERE Type = 'Expense' AND IsSystem = 0;");

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "Name", "Type", "AccountNumber", "IsBankAccount" },
                values: new object[] { "Cash", "Income", "0100", false });

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                columns: new[] { "Type", "AccountNumber" },
                values: new object[] { "Income", "0101" });

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                columns: new[] { "AccountNumber" },
                values: new object[] { "9900" });

            migrationBuilder.DropColumn(
                name: "FinancialYearStartMonth",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "IsBankAccount",
                table: "Accounts");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions",
                newName: "IX_Transactions_CategoryId");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "Transactions",
                newName: "CategoryId");

            migrationBuilder.RenameColumn(
                name: "AccountNumber",
                table: "Accounts",
                newName: "GLAccount");

            migrationBuilder.RenameTable(
                name: "Accounts",
                newName: "Categories");
        }
    }
}
