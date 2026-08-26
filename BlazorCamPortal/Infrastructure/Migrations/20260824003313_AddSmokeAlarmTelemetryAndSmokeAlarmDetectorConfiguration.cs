using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSmokeAlarmTelemetryAndSmokeAlarmDetectorConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmokeAlarmDetectorConfigurations",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinBatterySOCForAlert = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmokeAlarmDetectorConfigurations", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_SmokeAlarmDetectorConfigurations_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmokeAlarmTelemetry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoggedTimeUTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Event = table.Column<int>(type: "int", nullable: false),
                    BatterySOCPercent = table.Column<double>(type: "float", nullable: false),
                    BatteryVoltage = table.Column<double>(type: "float", nullable: false),
                    IsCharging = table.Column<bool>(type: "bit", nullable: false),
                    BootCount = table.Column<int>(type: "int", nullable: false),
                    DetectedAlarmBeepCount = table.Column<int>(type: "int", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmokeAlarmTelemetry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmokeAlarmTelemetry_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmokeAlarmTelemetry_DeviceId",
                table: "SmokeAlarmTelemetry",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_SmokeAlarmTelemetry_LoggedTimeUTC",
                table: "SmokeAlarmTelemetry",
                column: "LoggedTimeUTC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmokeAlarmDetectorConfigurations");

            migrationBuilder.DropTable(
                name: "SmokeAlarmTelemetry");
        }
    }
}
