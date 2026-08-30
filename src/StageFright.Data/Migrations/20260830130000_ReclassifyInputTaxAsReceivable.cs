using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
    /// <summary>
    /// Spec 028 Phase 16 / issue #355. Reclassifies the seeded system account
    /// <c>2320</c> from <c>AccountType.Liability</c> "Tax Paid" to
    /// <c>AccountType.Asset</c> "Tax Receivable": tax paid on purchases is
    /// recoverable from the tax authority, so it is an asset (a receivable), not a
    /// liability. Classification and presentation only — no stored monetary amount,
    /// tax amount, <c>TaxCode</c> value or GL line is touched, so the AUD zero-drift
    /// regression still holds. The account number stays <c>2320</c> as a documented
    /// asset exception; renumbering would desync the denormalized
    /// <c>Transaction.GLAccount</c> snapshot on historical rows.
    /// </summary>
    /// <inheritdoc />
    public partial class ReclassifyInputTaxAsReceivable : Migration
    {
        private static readonly Guid TaxAccountId = new("00000000-0000-0000-0000-000000000005");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: TaxAccountId,
                columns: new[] { "Name", "Type" },
                values: new object[] { "Tax Receivable", "Asset" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: TaxAccountId,
                columns: new[] { "Name", "Type" },
                values: new object[] { "Tax Paid", "Liability" });
        }
    }
}
