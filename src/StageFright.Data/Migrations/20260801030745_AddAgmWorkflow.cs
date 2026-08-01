using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgmWorkflow : Migration
    {
        private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastCommitteeResetYear",
                table: "Settings");

            migrationBuilder.AddColumn<int>(
                name: "GeneralCommitteeSeatCountTarget",
                table: "Settings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnnualGeneralMeetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    GeneralCommitteeSeatCountTarget = table.Column<int>(type: "INTEGER", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualGeneralMeetings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommitteeOfficeHolderTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeOfficeHolderTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommitteeTerms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedByAgmId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LabelYear = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitteeTerms_AnnualGeneralMeetings_StartedByAgmId",
                        column: x => x.StartedByAgmId,
                        principalTable: "AnnualGeneralMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgmAttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnnualGeneralMeetingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Attended = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgmAttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgmAttendanceRecords_AnnualGeneralMeetings_AnnualGeneralMeetingId",
                        column: x => x.AnnualGeneralMeetingId,
                        principalTable: "AnnualGeneralMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgmAttendanceRecords_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Rename in place (preserves existing CommitteeMembership rows and their PK/FK on upgrade)
            // rather than drop+recreate, per data-model.md's "renamed + extended" contract.
            migrationBuilder.RenameTable(
                name: "CommitteeMemberships",
                newName: "CommitteePositionRecords");

            migrationBuilder.DropIndex(
                name: "IX_CommitteeMemberships_MemberId_Year",
                table: "CommitteePositionRecords");

            migrationBuilder.AlterColumn<int>(
                name: "Year",
                table: "CommitteePositionRecords",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "Position",
                table: "CommitteePositionRecords",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<Guid>(
                name: "CommitteeTermId",
                table: "CommitteePositionRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OfficeHolderTypeId",
                table: "CommitteePositionRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "CommitteePositionRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "CommitteePositionRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CommitteePositionRecords_CommitteeOfficeHolderTypes_OfficeHolderTypeId",
                table: "CommitteePositionRecords",
                column: "OfficeHolderTypeId",
                principalTable: "CommitteeOfficeHolderTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CommitteePositionRecords_CommitteeTerms_CommitteeTermId",
                table: "CommitteePositionRecords",
                column: "CommitteeTermId",
                principalTable: "CommitteeTerms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_AgmAttendanceRecords_AnnualGeneralMeetingId_MemberId",
                table: "AgmAttendanceRecords",
                columns: new[] { "AnnualGeneralMeetingId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgmAttendanceRecords_MemberId",
                table: "AgmAttendanceRecords",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeOfficeHolderTypes_Name",
                table: "CommitteeOfficeHolderTypes",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteePositionRecords_CommitteeTermId_MemberId",
                table: "CommitteePositionRecords",
                columns: new[] { "CommitteeTermId", "MemberId" },
                unique: true,
                filter: "[EndDate] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteePositionRecords_CommitteeTermId_OfficeHolderTypeId",
                table: "CommitteePositionRecords",
                columns: new[] { "CommitteeTermId", "OfficeHolderTypeId" },
                unique: true,
                filter: "[EndDate] IS NULL AND [OfficeHolderTypeId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteePositionRecords_MemberId",
                table: "CommitteePositionRecords",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteePositionRecords_OfficeHolderTypeId",
                table: "CommitteePositionRecords",
                column: "OfficeHolderTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeTerms_StartedByAgmId",
                table: "CommitteeTerms",
                column: "StartedByAgmId");

            // Seed the 3 built-in office-holder titles so both new and upgrading installs get them
            // without a separate seeding step (research D — mirrors SeedSystemAccounts' fixed-timestamp convention).
            migrationBuilder.InsertData(
                table: "CommitteeOfficeHolderTypes",
                columns: new[] { "Id", "Name", "DisplayOrder", "IsBuiltIn", "IsDeleted", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000101"), "President", 0, true, false, SeedTimestamp, SeedTimestamp },
                    { new Guid("00000000-0000-0000-0000-000000000102"), "Secretary", 1, true, false, SeedTimestamp, SeedTimestamp },
                    { new Guid("00000000-0000-0000-0000-000000000103"), "Treasurer", 2, true, false, SeedTimestamp, SeedTimestamp }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CommitteeOfficeHolderTypes",
                keyColumn: "Id",
                keyValues: new object[]
                {
                    new Guid("00000000-0000-0000-0000-000000000101"),
                    new Guid("00000000-0000-0000-0000-000000000102"),
                    new Guid("00000000-0000-0000-0000-000000000103")
                });

            migrationBuilder.DropTable(
                name: "AgmAttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_CommitteePositionRecords_CommitteeOfficeHolderTypes_OfficeHolderTypeId",
                table: "CommitteePositionRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_CommitteePositionRecords_CommitteeTerms_CommitteeTermId",
                table: "CommitteePositionRecords");

            migrationBuilder.DropTable(
                name: "CommitteeOfficeHolderTypes");

            migrationBuilder.DropTable(
                name: "CommitteeTerms");

            migrationBuilder.DropTable(
                name: "AnnualGeneralMeetings");

            migrationBuilder.DropIndex(
                name: "IX_CommitteePositionRecords_CommitteeTermId_MemberId",
                table: "CommitteePositionRecords");

            migrationBuilder.DropIndex(
                name: "IX_CommitteePositionRecords_CommitteeTermId_OfficeHolderTypeId",
                table: "CommitteePositionRecords");

            migrationBuilder.DropIndex(
                name: "IX_CommitteePositionRecords_MemberId",
                table: "CommitteePositionRecords");

            migrationBuilder.DropIndex(
                name: "IX_CommitteePositionRecords_OfficeHolderTypeId",
                table: "CommitteePositionRecords");

            migrationBuilder.DropColumn(
                name: "CommitteeTermId",
                table: "CommitteePositionRecords");

            migrationBuilder.DropColumn(
                name: "OfficeHolderTypeId",
                table: "CommitteePositionRecords");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "CommitteePositionRecords");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "CommitteePositionRecords");

            migrationBuilder.AlterColumn<int>(
                name: "Year",
                table: "CommitteePositionRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Position",
                table: "CommitteePositionRecords",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.RenameTable(
                name: "CommitteePositionRecords",
                newName: "CommitteeMemberships");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMemberships_MemberId_Year",
                table: "CommitteeMemberships",
                columns: new[] { "MemberId", "Year" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.DropColumn(
                name: "GeneralCommitteeSeatCountTarget",
                table: "Settings");

            migrationBuilder.AddColumn<int>(
                name: "LastCommitteeResetYear",
                table: "Settings",
                type: "INTEGER",
                nullable: true);
        }
    }
}
