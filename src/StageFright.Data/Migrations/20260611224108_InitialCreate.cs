using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StageFright.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditTrailEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTrailEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    GLAccount = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsSystemDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    StreetAddress = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    JoinDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ActivateDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InactivateDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rehearsals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Time = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    StoredAttendanceRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rehearsals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationName = table.Column<string>(type: "TEXT", nullable: false),
                    AnnualFee = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AttendanceFee = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MembershipRenewalMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    CommitteeRenewalMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxAgeRangeYears = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumMemberAge = table.Column<int>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", nullable: false),
                    LastCommitteeResetYear = table.Column<int>(type: "INTEGER", nullable: true),
                    SchemaVersion = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    StoredParticipationRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_EventTypes_EventTypeId",
                        column: x => x.EventTypeId,
                        principalTable: "EventTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommitteeMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitteeMemberships_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "TEXT", nullable: false),
                    PaymentType = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RehearsalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Attended = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Rehearsals_RehearsalId",
                        column: x => x.RehearsalId,
                        principalTable: "Rehearsals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Fees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FeeType = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    FeeDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PaidAtCreation = table.Column<bool>(type: "INTEGER", nullable: false),
                    RehearsalId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fees_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fees_Rehearsals_RehearsalId",
                        column: x => x.RehearsalId,
                        principalTable: "Rehearsals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParticipationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Participated = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipationRecords_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParticipationRecords_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DebitAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    GLAccount = table.Column<string>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PaymentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FeeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Fees_FeeId",
                        column: x => x.FeeId,
                        principalTable: "Fees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "DeletedBy", "GLAccount", "IsDeleted", "IsSystem", "Name", "SortOrder", "Type", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "0100", false, true, "Cash", 0, "Income", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "0101", false, true, "Member Receivable", 1, "Income", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "9900", false, true, "Bad Debt Expense", 999, "Expense", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_MemberId",
                table: "AttendanceRecords",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_RehearsalId_MemberId",
                table: "AttendanceRecords",
                columns: new[] { "RehearsalId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMemberships_MemberId_Year",
                table: "CommitteeMemberships",
                columns: new[] { "MemberId", "Year" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventTypeId",
                table: "Events",
                column: "EventTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EventTypes_Name",
                table: "EventTypes",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Fees_MemberId",
                table: "Fees",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Fees_RehearsalId",
                table: "Fees",
                column: "RehearsalId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipationRecords_EventId_MemberId",
                table: "ParticipationRecords",
                columns: new[] { "EventId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParticipationRecords_MemberId",
                table: "ParticipationRecords",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MemberId",
                table: "Payments",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CategoryId",
                table: "Transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_FeeId",
                table: "Transactions",
                column: "FeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_MemberId",
                table: "Transactions",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PaymentId",
                table: "Transactions",
                column: "PaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "AuditTrailEntries");

            migrationBuilder.DropTable(
                name: "CommitteeMemberships");

            migrationBuilder.DropTable(
                name: "ParticipationRecords");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Fees");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "EventTypes");

            migrationBuilder.DropTable(
                name: "Rehearsals");

            migrationBuilder.DropTable(
                name: "Members");
        }
    }
}
