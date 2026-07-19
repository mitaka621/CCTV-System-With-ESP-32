using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemetryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CameraTelemetry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fps = table.Column<double>(type: "float", nullable: false),
                    AvgCaptureMs = table.Column<int>(type: "int", nullable: false),
                    MaxCaptureMs = table.Column<int>(type: "int", nullable: false),
                    AvgEncryptMs = table.Column<int>(type: "int", nullable: false),
                    MaxEncryptMs = table.Column<int>(type: "int", nullable: false),
                    AvgSendMs = table.Column<int>(type: "int", nullable: false),
                    MaxSendMs = table.Column<int>(type: "int", nullable: false),
                    AvgFrameKB = table.Column<int>(type: "int", nullable: false),
                    MaxFrameKB = table.Column<int>(type: "int", nullable: false),
                    BufferReadyPercent = table.Column<int>(type: "int", nullable: false),
                    FrameCount = table.Column<long>(type: "bigint", nullable: false),
                    FailedSends = table.Column<long>(type: "bigint", nullable: false),
                    CaptureFailures = table.Column<long>(type: "bigint", nullable: false),
                    LightSensorValue = table.Column<int>(type: "int", nullable: false),
                    IsNight = table.Column<bool>(type: "bit", nullable: false),
                    LightSensorPresent = table.Column<bool>(type: "bit", nullable: false),
                    TemperatureC = table.Column<double>(type: "float", nullable: false),
                    HumidityPercent = table.Column<double>(type: "float", nullable: false),
                    DewPointC = table.Column<double>(type: "float", nullable: false),
                    TempHumiditySensorPresent = table.Column<bool>(type: "bit", nullable: false),
                    MotionSensorPresent = table.Column<bool>(type: "bit", nullable: false),
                    CaseSwitchPresent = table.Column<bool>(type: "bit", nullable: false),
                    CaseOpen = table.Column<bool>(type: "bit", nullable: false),
                    MotionActive = table.Column<bool>(type: "bit", nullable: false),
                    MotionEvents = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraTelemetry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CameraTelemetry_Devices_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CameraTelemetry_CameraId_TimestampUtc",
                table: "CameraTelemetry",
                columns: new[] { "CameraId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CameraTelemetry");
        }
    }
}
