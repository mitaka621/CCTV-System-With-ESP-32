using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVideoChunkIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VideoChunks_DeviceId",
                table: "VideoChunks");

            migrationBuilder.CreateIndex(
                name: "IX_VideoChunks_DeviceId_ChunkStartTime",
                table: "VideoChunks",
                columns: new[] { "DeviceId", "ChunkStartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VideoChunks_DeviceId_ChunkStartTime",
                table: "VideoChunks");

            migrationBuilder.CreateIndex(
                name: "IX_VideoChunks_DeviceId",
                table: "VideoChunks",
                column: "DeviceId");
        }
    }
}
