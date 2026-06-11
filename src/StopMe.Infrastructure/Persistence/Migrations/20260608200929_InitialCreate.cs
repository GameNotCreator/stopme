using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StopMe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Blocklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocklists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryDomains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    MachineIdHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SyncEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PausePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    FocusMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    BreakMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    DailyAllowanceMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    CountOnlyWhenActiveWindow = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequireMouseActivity = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxPausesPerSession = table.Column<int>(type: "INTEGER", nullable: true),
                    PauseDurationMinutes = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PausePolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "UsageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RuleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    DomainHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    AppNameHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    DisplayLabel = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    IsLocalOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlockRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BlocklistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlockRules_Blocklists_BlocklistId",
                        column: x => x.BlocklistId,
                        principalTable: "Blocklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FocusSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    LockPolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRecurring = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecurrenceRule = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AllowPauses = table.Column<bool>(type: "INTEGER", nullable: false),
                    PausePolicyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    RequiredText = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FocusSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FocusSessions_PausePolicies_PausePolicyId",
                        column: x => x.PausePolicyId,
                        principalTable: "PausePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SessionBlocklists",
                columns: table => new
                {
                    FocusSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BlocklistId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionBlocklists", x => new { x.FocusSessionId, x.BlocklistId });
                    table.ForeignKey(
                        name: "FK_SessionBlocklists_Blocklists_BlocklistId",
                        column: x => x.BlocklistId,
                        principalTable: "Blocklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionBlocklists_FocusSessions_FocusSessionId",
                        column: x => x.FocusSessionId,
                        principalTable: "FocusSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Blocklists_UserProfileId",
                table: "Blocklists",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Blocklists_UserProfileId_Name",
                table: "Blocklists",
                columns: new[] { "UserProfileId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlockRules_BlocklistId",
                table: "BlockRules",
                column: "BlocklistId");

            migrationBuilder.CreateIndex(
                name: "IX_BlockRules_BlocklistId_Type",
                table: "BlockRules",
                columns: new[] { "BlocklistId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryDomains_CategoryName",
                table: "CategoryDomains",
                column: "CategoryName");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryDomains_CategoryName_Domain",
                table: "CategoryDomains",
                columns: new[] { "CategoryName", "Domain" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_MachineIdHash",
                table: "Devices",
                column: "MachineIdHash");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserProfileId",
                table: "Devices",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_FocusSessions_IsRecurring_Status",
                table: "FocusSessions",
                columns: new[] { "IsRecurring", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FocusSessions_PausePolicyId",
                table: "FocusSessions",
                column: "PausePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_FocusSessions_StartAtUtc",
                table: "FocusSessions",
                column: "StartAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FocusSessions_UserProfileId",
                table: "FocusSessions",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionBlocklists_BlocklistId",
                table: "SessionBlocklists",
                column: "BlocklistId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_OccurredAtUtc",
                table: "UsageEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_SessionId",
                table: "UsageEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_Type_OccurredAtUtc",
                table: "UsageEvents",
                columns: new[] { "Type", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockRules");

            migrationBuilder.DropTable(
                name: "CategoryDomains");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "SessionBlocklists");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "UsageEvents");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "Blocklists");

            migrationBuilder.DropTable(
                name: "FocusSessions");

            migrationBuilder.DropTable(
                name: "PausePolicies");
        }
    }
}
