using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class RemoveUserDeviceTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDevices");

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionOrganizationName",
                table: "Devices",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SubscriptionOrganizationName",
                table: "Devices",
                column: "SubscriptionOrganizationName");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Subscriptions_SubscriptionOrganizationName",
                table: "Devices",
                column: "SubscriptionOrganizationName",
                principalTable: "Subscriptions",
                principalColumn: "OrganizationName",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Subscriptions_SubscriptionOrganizationName",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_SubscriptionOrganizationName",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SubscriptionOrganizationName",
                table: "Devices");

            migrationBuilder.CreateTable(
                name: "UserDevices",
                columns: table => new
                {
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    DroHubUserId = table.Column<string>(type: "varchar(255) CHARACTER SET utf8mb4", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDevices", x => new { x.DeviceId, x.DroHubUserId });
                    table.ForeignKey(
                        name: "FK_UserDevices_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserDevices_AspNetUsers_DroHubUserId",
                        column: x => x.DroHubUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_DroHubUserId",
                table: "UserDevices",
                column: "DroHubUserId");
        }
    }
}
