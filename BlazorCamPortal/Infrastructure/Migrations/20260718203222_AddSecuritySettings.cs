using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSecuritySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaseSwitchPresent",
                table: "CameraTelemetry");

            migrationBuilder.AddColumn<double>(
                name: "SecurityMaxHumidityPercent",
                table: "SystemSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SecurityMaxTemperatureC",
                table: "SystemSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "SecurityMinFps",
                table: "SystemSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

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
                columns: new[] { "SecurityMaxHumidityPercent", "SecurityMaxTemperatureC", "SecurityMinFps" },
                values: new object[] { 80.0, 60.0, 4 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityMaxHumidityPercent",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "SecurityMaxTemperatureC",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "SecurityMinFps",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "CaseSensorInstalled",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SecurityArmed",
                table: "Devices");

            migrationBuilder.AddColumn<bool>(
                name: "CaseSwitchPresent",
                table: "CameraTelemetry",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
