using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExportedVideosAndSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExportedVideos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExportedURLForDownload = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ExportStartedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExportFinishedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExportStatus = table.Column<int>(type: "int", nullable: false),
                    VideoStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VideoEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SizeInMB = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportedVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportedVideos_Devices_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExportedVideos_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncodedVideoRetention = table.Column<int>(type: "int", nullable: false),
                    CameraChunkRetention = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Id", "CameraChunkRetention", "EncodedVideoRetention" },
                values: new object[] { new Guid("8f4d2a1c-0b6e-4c3a-9d57-1f2e3a4b5c6d"), 3, 5 });

            migrationBuilder.CreateIndex(
                name: "IX_ExportedVideos_CameraId",
                table: "ExportedVideos",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportedVideos_ExportFinishedDate",
                table: "ExportedVideos",
                column: "ExportFinishedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ExportedVideos_UserId",
                table: "ExportedVideos",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportedVideos");

            migrationBuilder.DropTable(
                name: "SystemSettings");
        }
    }
}
