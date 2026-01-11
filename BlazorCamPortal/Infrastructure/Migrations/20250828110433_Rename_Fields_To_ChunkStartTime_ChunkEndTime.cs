using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Rename_Fields_To_ChunkStartTime_ChunkEndTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChunkStartDate",
                table: "VideoChunks",
                newName: "ChunkStartTime");

            migrationBuilder.RenameColumn(
                name: "ChunkEndDate",
                table: "VideoChunks",
                newName: "ChunkEndTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChunkStartTime",
                table: "VideoChunks",
                newName: "ChunkStartDate");

            migrationBuilder.RenameColumn(
                name: "ChunkEndTime",
                table: "VideoChunks",
                newName: "ChunkEndDate");
        }
    }
}
