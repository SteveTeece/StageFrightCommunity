using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFright.Data.Migrations
{
    /// <summary>
    /// Splits the single Members.Name column into FirstName + LastName. Backfills existing
    /// rows via pure SQL (trim -> collapse internal whitespace -> split on first space ->
    /// truncate each side to 100 chars), matching StageFright.Core.Modules.Members.MemberNameSplitter's
    /// C# implementation used elsewhere (backup restore, unit tests). A single-word Name yields
    /// LastName = '' (FR-008) rather than losing the member. Hand-edited after scaffolding,
    /// following the ConvertCategoriesToAccounts precedent for data-transforming migrations.
    /// </summary>
    public partial class SplitMemberNameIntoFirstLastName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add staging columns nullable — backfill runs before NOT NULL is enforced.
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Members",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Members",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            // 2. Normalize: trim outer whitespace.
            migrationBuilder.Sql("UPDATE Members SET Name = TRIM(Name);");

            // 3. Collapse runs of internal spaces to one (10 passes safely converges for any
            //    realistic name length — each pass roughly halves the longest space run).
            for (var i = 0; i < 10; i++)
            {
                migrationBuilder.Sql("UPDATE Members SET Name = REPLACE(Name, '  ', ' ') WHERE Name LIKE '%  %';");
            }

            // 4. Split on first space; truncate each side to 100 chars. Mononym -> LastName = ''.
            migrationBuilder.Sql(
                "UPDATE Members SET " +
                "FirstName = CASE WHEN INSTR(Name, ' ') = 0 " +
                "THEN SUBSTR(Name, 1, 100) " +
                "ELSE SUBSTR(SUBSTR(Name, 1, INSTR(Name, ' ') - 1), 1, 100) END, " +
                "LastName = CASE WHEN INSTR(Name, ' ') = 0 " +
                "THEN '' " +
                "ELSE SUBSTR(SUBSTR(Name, INSTR(Name, ' ') + 1), 1, 100) END;");

            // 5. Every row is now populated — lock the columns down (SQLite provider rebuilds
            //    the table automatically for this operation).
            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Members",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Members",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldNullable: true);

            // 6. Drop the old combined column.
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Members");

            // No Settings.SchemaVersion bump — additive/structural change, same shape as every
            // migration since ConvertCategoriesToAccounts other than that one itself.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Members",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            // Approximate, not exact, inverse — irregular original whitespace/casing isn't
            // recoverable. Down() migrations here are a dev/rollback safety net, not a
            // guaranteed lossless inverse (same precedent as ConvertCategoriesToAccounts.Down()).
            migrationBuilder.Sql("UPDATE Members SET Name = TRIM(FirstName || ' ' || LastName);");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Members",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Members");
        }
    }
}
