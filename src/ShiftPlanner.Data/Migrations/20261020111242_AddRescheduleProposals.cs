using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftPlanner.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRescheduleProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RescheduleProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaveStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    LeaveEnd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RescheduleProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RescheduleProposals_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProposedChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Shift = table.Column<int>(type: "INTEGER", nullable: false),
                    ProposalId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposedChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposedChanges_RescheduleProposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "RescheduleProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposedChanges_ProposalId",
                table: "ProposedChanges",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_RescheduleProposals_ScheduleId",
                table: "RescheduleProposals",
                column: "ScheduleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposedChanges");

            migrationBuilder.DropTable(
                name: "RescheduleProposals");
        }
    }
}
