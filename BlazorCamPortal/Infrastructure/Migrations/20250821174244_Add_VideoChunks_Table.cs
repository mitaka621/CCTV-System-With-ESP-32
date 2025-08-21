using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorCamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_VideoChunks_Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ChunkStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChunkEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoChunks_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoChunks_CameraId",
                table: "VideoChunks",
                column: "CameraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoChunks");
        }
    }
}
