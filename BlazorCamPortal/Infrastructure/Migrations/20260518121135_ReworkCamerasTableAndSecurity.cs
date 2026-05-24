using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReworkCamerasTableAndSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoChunks_Cameras_CameraId",
                table: "VideoChunks");

            migrationBuilder.DropTable(
                name: "Cameras");

            migrationBuilder.RenameColumn(
                name: "CameraId",
                table: "VideoChunks",
                newName: "DeviceId");

            migrationBuilder.RenameIndex(
                name: "IX_VideoChunks_CameraId",
                table: "VideoChunks",
                newName: "IX_VideoChunks_DeviceId");

            migrationBuilder.CreateTable(
                name: "DeviceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IconName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IconUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeviceVariant = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ipv4Address = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MacAddress = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PairStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SessionToken = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SessionTokenExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PublicKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_DeviceTypes_DeviceTypeId",
                        column: x => x.DeviceTypeId,
                        principalTable: "DeviceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreprovisionAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nonce = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimedFromIpv4 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PreprovisionStatus = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreprovisionAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreprovisionAttempts_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceTypeId",
                table: "Devices",
                column: "DeviceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_PairStatus",
                table: "Devices",
                column: "PairStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PreprovisionAttempts_DeviceId_PreprovisionStatus",
                table: "PreprovisionAttempts",
                columns: new[] { "DeviceId", "PreprovisionStatus" });

            migrationBuilder.AddForeignKey(
                name: "FK_VideoChunks_Devices_DeviceId",
                table: "VideoChunks",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoChunks_Devices_DeviceId",
                table: "VideoChunks");

            migrationBuilder.DropTable(
                name: "PreprovisionAttempts");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "DeviceTypes");

            migrationBuilder.RenameColumn(
                name: "DeviceId",
                table: "VideoChunks",
                newName: "CameraId");

            migrationBuilder.RenameIndex(
                name: "IX_VideoChunks_DeviceId",
                table: "VideoChunks",
                newName: "IX_VideoChunks_CameraId");

            migrationBuilder.CreateTable(
                name: "Cameras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Ipv4Address = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MacAddress = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PairStatus = table.Column<int>(type: "int", nullable: false),
                    SessionToken = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SessionTokenExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cameras", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_Ipv4Address",
                table: "Cameras",
                column: "Ipv4Address");

            migrationBuilder.Sql("DELETE FROM [VideoChunks];");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoChunks_Cameras_CameraId",
                table: "VideoChunks",
                column: "CameraId",
                principalTable: "Cameras",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }
    }
}
