using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
    /// <inheritdoc />
    public partial class GenericSalesTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Abn",
                table: "Settings");

            migrationBuilder.RenameColumn(
                name: "GstCode",
                table: "Transactions",
                newName: "TaxCode");

            migrationBuilder.RenameColumn(
                name: "IsGstRegistered",
                table: "Settings",
                newName: "IsTaxApplicable");

            migrationBuilder.RenameColumn(
                name: "AttendanceFeeGstCode",
                table: "Settings",
                newName: "AttendanceFeeTaxCode");

            migrationBuilder.RenameColumn(
                name: "AnnualFeeGstCode",
                table: "Settings",
                newName: "AnnualFeeTaxCode");

            migrationBuilder.RenameColumn(
                name: "GstCode",
                table: "Fees",
                newName: "TaxCode");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "Settings",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: true);

            // Value remap: the retired 4-value GstCode is stored as its member name
            // (HasConversion<string>()), not an ordinal, so historical rows need their stored
            // string rewritten to the new 3-value TaxCode's member names — never their dollar
            // amounts, which this migration never touches. See specs/016-generic-sales-tax/data-model.md.
            foreach (var table in new[] { "Transactions", "Fees" })
            {
                migrationBuilder.Sql($"UPDATE \"{table}\" SET \"TaxCode\" = 'Taxable' WHERE \"TaxCode\" = 'Gst';");
                migrationBuilder.Sql($"UPDATE \"{table}\" SET \"TaxCode\" = 'TaxExempt' WHERE \"TaxCode\" IN ('GstFree', 'InputTaxed');");
                migrationBuilder.Sql($"UPDATE \"{table}\" SET \"TaxCode\" = 'Excluded' WHERE \"TaxCode\" = 'BasExcluded';");
            }

            foreach (var column in new[] { "AnnualFeeTaxCode", "AttendanceFeeTaxCode" })
            {
                migrationBuilder.Sql($"UPDATE \"Settings\" SET \"{column}\" = 'Taxable' WHERE \"{column}\" = 'Gst';");
                migrationBuilder.Sql($"UPDATE \"Settings\" SET \"{column}\" = 'TaxExempt' WHERE \"{column}\" IN ('GstFree', 'InputTaxed');");
                migrationBuilder.Sql($"UPDATE \"Settings\" SET \"{column}\" = 'Excluded' WHERE \"{column}\" = 'BasExcluded';");
            }

            // Orgs that were GST-registered had an implicit hardcoded 10% rate (GstConstants.Rate)
            // before this feature — carry that forward as their explicit starting TaxRate.
            migrationBuilder.Sql("UPDATE \"Settings\" SET \"TaxRate\" = 10 WHERE \"IsTaxApplicable\" = 1;");

            // System account display names — GUIDs/account numbers are unchanged (see research.md).
            migrationBuilder.Sql("UPDATE \"Accounts\" SET \"Name\" = 'Tax Collected' WHERE \"Id\" = '00000000-0000-0000-0000-000000000004';");
            migrationBuilder.Sql("UPDATE \"Accounts\" SET \"Name\" = 'Tax Paid' WHERE \"Id\" = '00000000-0000-0000-0000-000000000005';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the system account display names and value remap before the columns
            // are renamed back, so the SQL below still targets the (still-current) new names.
            migrationBuilder.Sql("UPDATE \"Accounts\" SET \"Name\" = 'GST Collected' WHERE \"Id\" = '00000000-0000-0000-0000-000000000004';");
            migrationBuilder.Sql("UPDATE \"Accounts\" SET \"Name\" = 'GST Paid' WHERE \"Id\" = '00000000-0000-0000-0000-000000000005';");

            foreach (var table in new[] { "Transactions", "Fees" })
            {
                migrationBuilder.Sql($"UPDATE \"{table}\" SET \"TaxCode\" = 'Gst' WHERE \"TaxCode\" = 'Taxable';");
                migrationBuilder.Sql($"UPDATE \"{table}\" SET \"TaxCode\" = 'GstFree' WHERE \"TaxCode\" = 'TaxExempt';");
                migrationBuilder.Sql($"UPDATE \"{table}\" SET \"TaxCode\" = 'BasExcluded' WHERE \"TaxCode\" = 'Excluded';");
            }

            foreach (var column in new[] { "AnnualFeeTaxCode", "AttendanceFeeTaxCode" })
            {
                migrationBuilder.Sql($"UPDATE \"Settings\" SET \"{column}\" = 'Gst' WHERE \"{column}\" = 'Taxable';");
                migrationBuilder.Sql($"UPDATE \"Settings\" SET \"{column}\" = 'GstFree' WHERE \"{column}\" = 'TaxExempt';");
                migrationBuilder.Sql($"UPDATE \"Settings\" SET \"{column}\" = 'BasExcluded' WHERE \"{column}\" = 'Excluded';");
            }

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "Settings");

            migrationBuilder.RenameColumn(
                name: "TaxCode",
                table: "Transactions",
                newName: "GstCode");

            migrationBuilder.RenameColumn(
                name: "IsTaxApplicable",
                table: "Settings",
                newName: "IsGstRegistered");

            migrationBuilder.RenameColumn(
                name: "AttendanceFeeTaxCode",
                table: "Settings",
                newName: "AttendanceFeeGstCode");

            migrationBuilder.RenameColumn(
                name: "AnnualFeeTaxCode",
                table: "Settings",
                newName: "AnnualFeeGstCode");

            migrationBuilder.RenameColumn(
                name: "TaxCode",
                table: "Fees",
                newName: "GstCode");

            migrationBuilder.AddColumn<string>(
                name: "Abn",
                table: "Settings",
                type: "TEXT",
                nullable: true);
        }
    }
}
