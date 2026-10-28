using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JustGo.Flights.GroundOperations.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ground_operations");

            migrationBuilder.CreateTable(
                name: "Turnarounds",
                schema: "ground_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartureUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Gate = table.Column<string>(type: "text", nullable: true),
                    GateCleared = table.Column<bool>(type: "boolean", nullable: false),
                    GateReleased = table.Column<bool>(type: "boolean", nullable: false),
                    CrewAccessScheduled = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turnarounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                schema: "ground_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnaroundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    ScheduledUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tasks_Turnarounds_TurnaroundId",
                        column: x => x.TurnaroundId,
                        principalSchema: "ground_operations",
                        principalTable: "Turnarounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TurnaroundId_Type",
                schema: "ground_operations",
                table: "Tasks",
                columns: new[] { "TurnaroundId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tasks",
                schema: "ground_operations");

            migrationBuilder.DropTable(
                name: "Turnarounds",
                schema: "ground_operations");
        }
    }
}
