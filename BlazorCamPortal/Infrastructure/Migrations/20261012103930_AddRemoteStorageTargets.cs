using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteStorageTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemoteStorageTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProtectedPassword = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSyncEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteStorageTargets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoChunkReplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemoteStorageTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VideoChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReplicatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoChunkReplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoChunkReplications_RemoteStorageTargets_RemoteStorageTargetId",
                        column: x => x.RemoteStorageTargetId,
                        principalTable: "RemoteStorageTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoChunkReplications_VideoChunks_VideoChunkId",
                        column: x => x.VideoChunkId,
                        principalTable: "VideoChunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoChunkReplications_RemoteStorageTargetId_VideoChunkId",
                table: "VideoChunkReplications",
                columns: new[] { "RemoteStorageTargetId", "VideoChunkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoChunkReplications_VideoChunkId",
                table: "VideoChunkReplications",
                column: "VideoChunkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoChunkReplications");

            migrationBuilder.DropTable(
                name: "RemoteStorageTargets");
        }
    }
}
