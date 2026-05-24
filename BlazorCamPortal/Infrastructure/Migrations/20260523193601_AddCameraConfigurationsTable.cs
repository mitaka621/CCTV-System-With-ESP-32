using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCameraConfigurationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CameraConfigurations",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrameRotation = table.Column<float>(type: "real", nullable: false),
                    ZoomFactor = table.Column<float>(type: "real", nullable: false),
                    ZoomStartX = table.Column<int>(type: "int", nullable: false),
                    ZoomStartY = table.Column<int>(type: "int", nullable: false),
                    Brightness = table.Column<float>(type: "real", nullable: false),
                    Contrast = table.Column<float>(type: "real", nullable: false),
                    FlipMode = table.Column<int>(type: "int", nullable: false),
                    SharpenFactor = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraConfigurations", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_CameraConfigurations_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CameraConfigurations");
        }
    }
}
