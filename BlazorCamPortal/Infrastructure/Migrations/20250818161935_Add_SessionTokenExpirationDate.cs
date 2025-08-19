using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorCamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_SessionTokenExpirationDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SessionTokenExpirationDate",
                table: "Cameras",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionTokenExpirationDate",
                table: "Cameras");
        }
    }
}
