using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedFromLocalStorageAndRemote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ReplicatedDate",
                table: "VideoChunkReplications",
                newName: "ReplicatedDateUTC");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeletedFromLocalStorage",
                table: "VideoChunks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "VideoChunkReplications",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeletedFromLocalStorage",
                table: "VideoChunks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "VideoChunkReplications");

            migrationBuilder.RenameColumn(
                name: "ReplicatedDateUTC",
                table: "VideoChunkReplications",
                newName: "ReplicatedDate");
        }
    }
}
