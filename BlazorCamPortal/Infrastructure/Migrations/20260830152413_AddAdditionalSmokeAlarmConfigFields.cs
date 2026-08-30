using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdditionalSmokeAlarmConfigFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ChargeSenseVoltageThreashold",
                table: "SmokeAlarmDetectorConfigurations",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MaxVoltageOverchargeWarning",
                table: "SmokeAlarmDetectorConfigurations",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargeSenseVoltageThreashold",
                table: "SmokeAlarmDetectorConfigurations");

            migrationBuilder.DropColumn(
                name: "MaxVoltageOverchargeWarning",
                table: "SmokeAlarmDetectorConfigurations");
        }
    }
}
