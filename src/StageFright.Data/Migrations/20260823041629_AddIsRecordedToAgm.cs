using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsRecordedToAgm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRecorded",
                table: "AnnualGeneralMeetings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Every AGM row created before this feature was recorded through the always-complete
            // legacy RecordAsync, so it is correctly backfilled as recorded rather than scheduled.
            migrationBuilder.Sql("UPDATE AnnualGeneralMeetings SET IsRecorded = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRecorded",
                table: "AnnualGeneralMeetings");
        }
    }
}
