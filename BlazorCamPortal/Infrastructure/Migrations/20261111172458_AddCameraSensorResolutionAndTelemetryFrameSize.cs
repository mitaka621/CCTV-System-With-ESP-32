using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCameraSensorResolutionAndTelemetryFrameSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FrameHeight",
                table: "CameraTelemetry",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FrameWidth",
                table: "CameraTelemetry",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SensorResolution",
                table: "CameraConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 7);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FrameHeight",
                table: "CameraTelemetry");

            migrationBuilder.DropColumn(
                name: "FrameWidth",
                table: "CameraTelemetry");

            migrationBuilder.DropColumn(
                name: "SensorResolution",
                table: "CameraConfigurations");
        }
    }
}
