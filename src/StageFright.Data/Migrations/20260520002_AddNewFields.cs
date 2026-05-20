using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
	/// <inheritdoc />
	public partial class AddNewFields : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Add PaidStatus to Attendance table
			migrationBuilder.AddColumn<string>(
				name: "PaidStatus",
				table: "Attendances",
				type: "TEXT",
				maxLength: 20,
				nullable: false,
				defaultValue: "Paid");

			// Add StoredAttendanceRate to Rehearsals table
			migrationBuilder.AddColumn<decimal>(
				name: "StoredAttendanceRate",
				table: "Rehearsals",
				type: "decimal(5,2)",
				nullable: false,
				defaultValue: 0m);

			// Add StoredParticipationRate to Events table
			migrationBuilder.AddColumn<decimal>(
				name: "StoredParticipationRate",
				table: "Events",
				type: "decimal(5,2)",
				nullable: false,
				defaultValue: 0m);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// Remove columns in reverse order
			migrationBuilder.DropColumn(
				name: "StoredParticipationRate",
				table: "Events");

			migrationBuilder.DropColumn(
				name: "StoredAttendanceRate",
				table: "Rehearsals");

			migrationBuilder.DropColumn(
				name: "PaidStatus",
				table: "Attendances");
		}
	}
}
