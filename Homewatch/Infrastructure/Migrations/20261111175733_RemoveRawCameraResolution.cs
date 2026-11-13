using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homewatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRawCameraResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolutionHeight",
                table: "CameraConfigurations");

            migrationBuilder.DropColumn(
                name: "ResolutionWidth",
                table: "CameraConfigurations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResolutionHeight",
                table: "CameraConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ResolutionWidth",
                table: "CameraConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
