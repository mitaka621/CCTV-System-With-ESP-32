using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSecuritySettingsToCameraConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaseSensorInstalled",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SecurityArmed",
                table: "Devices");

            migrationBuilder.AddColumn<bool>(
                name: "CaseSensorInstalled",
                table: "CameraConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "MovementThresholdOffset",
                table: "CameraConfigurations",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RotationThresholdOffset",
                table: "CameraConfigurations",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "SecurityArmed",
                table: "CameraConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: new Guid("8f4d2a1c-0b6e-4c3a-9d57-1f2e3a4b5c6d"),
                columns: new[] { "SecurityMaxHumidityPercent", "SecurityMaxTemperatureC" },
                values: new object[] { 70.0, 90.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaseSensorInstalled",
                table: "CameraConfigurations");

            migrationBuilder.DropColumn(
                name: "MovementThresholdOffset",
                table: "CameraConfigurations");

            migrationBuilder.DropColumn(
                name: "RotationThresholdOffset",
                table: "CameraConfigurations");

            migrationBuilder.DropColumn(
                name: "SecurityArmed",
                table: "CameraConfigurations");

            migrationBuilder.AddColumn<bool>(
                name: "CaseSensorInstalled",
                table: "Devices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SecurityArmed",
                table: "Devices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: new Guid("8f4d2a1c-0b6e-4c3a-9d57-1f2e3a4b5c6d"),
                columns: new[] { "SecurityMaxHumidityPercent", "SecurityMaxTemperatureC" },
                values: new object[] { 80.0, 60.0 });
        }
    }
}
