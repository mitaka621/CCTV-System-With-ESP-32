using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLogMessagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimestampUTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LogLevel = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Exception = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogMessages_TimestampUTC",
                table: "LogMessages",
                column: "TimestampUTC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogMessages");
        }
    }
}
