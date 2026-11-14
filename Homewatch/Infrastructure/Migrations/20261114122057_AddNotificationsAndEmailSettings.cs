using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homewatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsAndEmailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DismissedNotificationRetention",
                table: "SystemSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EmailNotificationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    SenderDisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProtectedAppPassword = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PortalBaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailNotificationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TopicIdentifier = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IconColor = table.Column<int>(type: "int", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    RelativeRedirectUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DateCreatedUTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotificationType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationsUsers",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDismissed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationsUsers", x => new { x.UserId, x.NotificationId });
                    table.ForeignKey(
                        name: "FK_NotificationsUsers_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationsUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: new Guid("8f4d2a1c-0b6e-4c3a-9d57-1f2e3a4b5c6d"),
                column: "DismissedNotificationRetention",
                value: 8);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TopicIdentifier",
                table: "Notifications",
                column: "TopicIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationsUsers_NotificationId_IsDismissed",
                table: "NotificationsUsers",
                columns: new[] { "NotificationId", "IsDismissed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailNotificationSettings");

            migrationBuilder.DropTable(
                name: "NotificationsUsers");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropColumn(
                name: "DismissedNotificationRetention",
                table: "SystemSettings");
        }
    }
}
