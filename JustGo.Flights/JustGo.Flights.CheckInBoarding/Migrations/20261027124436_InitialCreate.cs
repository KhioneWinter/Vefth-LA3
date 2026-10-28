using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JustGo.Flights.CheckInBoarding.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "checkin_boarding");

            migrationBuilder.CreateTable(
                name: "Flights",
                schema: "checkin_boarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartureUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Gate = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flights", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Flights",
                schema: "checkin_boarding");
        }
    }
}
