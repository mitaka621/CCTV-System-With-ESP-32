using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCameraLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DeviceVariant",
                table: "DeviceTypes",
                newName: "DeviceCategory");

            migrationBuilder.CreateTable(
                name: "UserCameraLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    X = table.Column<int>(type: "int", nullable: false),
                    Y = table.Column<int>(type: "int", nullable: false),
                    LayoutType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCameraLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCameraLayouts_Devices_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCameraLayouts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCameraLayouts_CameraId",
                table: "UserCameraLayouts",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCameraLayouts_UserId_CameraId",
                table: "UserCameraLayouts",
                columns: new[] { "UserId", "CameraId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCameraLayouts");

            migrationBuilder.RenameColumn(
                name: "DeviceCategory",
                table: "DeviceTypes",
                newName: "DeviceVariant");
        }
    }
}
