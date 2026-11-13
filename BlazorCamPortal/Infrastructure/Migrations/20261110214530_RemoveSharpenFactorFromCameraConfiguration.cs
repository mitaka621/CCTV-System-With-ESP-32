using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homewatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSharpenFactorFromCameraConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SharpenFactor",
                table: "CameraConfigurations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "SharpenFactor",
                table: "CameraConfigurations",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }
    }
}
