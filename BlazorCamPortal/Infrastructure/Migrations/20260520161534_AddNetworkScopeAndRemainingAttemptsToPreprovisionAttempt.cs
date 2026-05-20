using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CamPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkScopeAndRemainingAttemptsToPreprovisionAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpectedNetworkIpv4",
                table: "PreprovisionAttempts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedSubnetMask",
                table: "PreprovisionAttempts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemainingAttempts",
                table: "PreprovisionAttempts",
                type: "int",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedNetworkIpv4",
                table: "PreprovisionAttempts");

            migrationBuilder.DropColumn(
                name: "ExpectedSubnetMask",
                table: "PreprovisionAttempts");

            migrationBuilder.DropColumn(
                name: "RemainingAttempts",
                table: "PreprovisionAttempts");
        }
    }
}
