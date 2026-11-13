using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homewatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceTargetToExportedVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceTargetId",
                table: "ExportedVideos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExportedVideos_SourceTargetId",
                table: "ExportedVideos",
                column: "SourceTargetId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExportedVideos_RemoteStorageTargets_SourceTargetId",
                table: "ExportedVideos",
                column: "SourceTargetId",
                principalTable: "RemoteStorageTargets",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExportedVideos_RemoteStorageTargets_SourceTargetId",
                table: "ExportedVideos");

            migrationBuilder.DropIndex(
                name: "IX_ExportedVideos_SourceTargetId",
                table: "ExportedVideos");

            migrationBuilder.DropColumn(
                name: "SourceTargetId",
                table: "ExportedVideos");
        }
    }
}
