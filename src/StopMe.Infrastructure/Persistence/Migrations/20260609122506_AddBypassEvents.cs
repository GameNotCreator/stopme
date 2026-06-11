using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StopMe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBypassEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BypassEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BypassEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BypassEvents_DetectedAtUtc",
                table: "BypassEvents",
                column: "DetectedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BypassEvents_SessionId",
                table: "BypassEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BypassEvents_Type_DetectedAtUtc",
                table: "BypassEvents",
                columns: new[] { "Type", "DetectedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BypassEvents");
        }
    }
}
