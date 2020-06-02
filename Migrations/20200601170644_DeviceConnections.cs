using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class DeviceConnections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DroneBatteryLevels_Devices_Serial",
                table: "DroneBatteryLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_DroneFlyingStates_Devices_Serial",
                table: "DroneFlyingStates");

            migrationBuilder.DropForeignKey(
                name: "FK_DroneRadioSignals_Devices_Serial",
                table: "DroneRadioSignals");

            migrationBuilder.DropForeignKey(
                name: "FK_DroneReplies_Devices_Serial",
                table: "DroneReplies");

            migrationBuilder.DropForeignKey(
                name: "FK_DroneVideoStatesResults_Devices_Serial",
                table: "DroneVideoStatesResults");

            migrationBuilder.DropForeignKey(
                name: "FK_Positions_Devices_Serial",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "LiveVideoSecret",
                table: "Devices");

            migrationBuilder.AlterColumn<string>(
                name: "Serial",
                table: "Positions",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(256) CHARACTER SET utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "ConnectionId",
                table: "Positions",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<string>(
                name: "Serial",
                table: "DroneVideoStatesResults",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(256) CHARACTER SET utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "ConnectionId",
                table: "DroneVideoStatesResults",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<string>(
                name: "Serial",
                table: "DroneReplies",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(256) CHARACTER SET utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "ConnectionId",
                table: "DroneReplies",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<string>(
                name: "Serial",
                table: "DroneRadioSignals",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(256) CHARACTER SET utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "ConnectionId",
                table: "DroneRadioSignals",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<string>(
                name: "Serial",
                table: "DroneFlyingStates",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(256) CHARACTER SET utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "ConnectionId",
                table: "DroneFlyingStates",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<string>(
                name: "Serial",
                table: "DroneBatteryLevels",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(256) CHARACTER SET utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "ConnectionId",
                table: "DroneBatteryLevels",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "DeviceConnections",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StartTime = table.Column<DateTime>(nullable: false),
                    EndTime = table.Column<DateTime>(nullable: false),
                    DeviceId = table.Column<int>(nullable: false),
                    SubscriptionOrganizationName = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceConnections_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceConnections_Subscriptions_SubscriptionOrganizationName",
                        column: x => x.SubscriptionOrganizationName,
                        principalTable: "Subscriptions",
                        principalColumn: "OrganizationName",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Positions_ConnectionId",
                table: "Positions",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DroneVideoStatesResults_ConnectionId",
                table: "DroneVideoStatesResults",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DroneReplies_ConnectionId",
                table: "DroneReplies",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DroneRadioSignals_ConnectionId",
                table: "DroneRadioSignals",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DroneFlyingStates_ConnectionId",
                table: "DroneFlyingStates",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DroneBatteryLevels_ConnectionId",
                table: "DroneBatteryLevels",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceConnections_DeviceId",
                table: "DeviceConnections",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceConnections_SubscriptionOrganizationName",
                table: "DeviceConnections",
                column: "SubscriptionOrganizationName");

            migrationBuilder.AddForeignKey(
                name: "FK_DroneBatteryLevels_DeviceConnections_ConnectionId",
                table: "DroneBatteryLevels",
                column: "ConnectionId",
                principalTable: "DeviceConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DroneFlyingStates_DeviceConnections_ConnectionId",
                table: "DroneFlyingStates",
                column: "ConnectionId",
                principalTable: "DeviceConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DroneRadioSignals_DeviceConnections_ConnectionId",
                table: "DroneRadioSignals",
                column: "ConnectionId",
                principalTable: "DeviceConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DroneReplies_DeviceConnections_ConnectionId",
                table: "DroneReplies",
                column: "ConnectionId",
                principalTable: "DeviceConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DroneVideoStatesResults_DeviceConnections_ConnectionId",
                table: "DroneVideoStatesResults",
                column: "ConnectionId",
                principalTable: "DeviceConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Positions_DeviceConnections_ConnectionId",
                table: "Positions",
                column: "ConnectionId",
                principalTable: "DeviceConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DroneBatteryLevels_DeviceConnections_ConnectionId",
                table: "DroneBatteryLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_DroneFlyingStates_DeviceConnections_ConnectionId",
                table: "DroneFlyingStates");

            migrationBuilder.DropForeignKey(
                name: "FK_DroneRadioSignals_DeviceConnections_ConnectionId",
                table: "DroneRadioSignals");

            migrationBuilder.DropForeignKey(
                name: "FK_DroneReplies_DeviceConnections_ConnectionId",
                table: "DroneReplies");

            migrationBuilder.DropForeignKey(
                name: "FK_DroneVideoStatesResults_DeviceConnections_ConnectionId",
                table: "DroneVideoStatesResults");

            migrationBuilder.DropForeignKey(
                name: "FK_Positions_DeviceConnections_ConnectionId",
                table: "Positions");

            migrationBuilder.DropTable(
                name: "DeviceConnections");

            migrationBuilder.DropIndex(
                name: "IX_Positions_ConnectionId",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_DroneVideoStatesResults_ConnectionId",
                table: "DroneVideoStatesResults");

            migrationBuilder.DropIndex(
                name: "IX_DroneReplies_ConnectionId",
                table: "DroneReplies");

            migrationBuilder.DropIndex(
                name: "IX_DroneRadioSignals_ConnectionId",
                table: "DroneRadioSignals");

            migrationBuilder.DropIndex(
                name: "IX_DroneFlyingStates_ConnectionId",
                table: "DroneFlyingStates");

            migrationBuilder.DropIndex(
                name: "IX_DroneBatteryLevels_ConnectionId",
                table: "DroneBatteryLevels");

            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "DroneVideoStatesResults");

            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "DroneReplies");

            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "DroneRadioSignals");

            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "DroneFlyingStates");

            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "DroneBatteryLevels");

            migrationBuilder.AlterColumn<string>(
                name: "Serial",
                table: "Positions",
                type: "varchar(256) CHARACTER SET utf8mb4",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Serial",
                table: "DroneVideoStatesResults",
                type: "varchar(256) CHARACTER SET utf8mb4",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Serial",
                table: "DroneReplies",
                type: "varchar(256) CHARACTER SET utf8mb4",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Serial",
                table: "DroneRadioSignals",
                type: "varchar(256) CHARACTER SET utf8mb4",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Serial",
                table: "DroneFlyingStates",
                type: "varchar(256) CHARACTER SET utf8mb4",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Serial",
                table: "DroneBatteryLevels",
                type: "varchar(256) CHARACTER SET utf8mb4",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiveVideoSecret",
                table: "Devices",
                type: "longtext CHARACTER SET utf8mb4",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Devices_SerialNumber",
                table: "Devices",
                column: "SerialNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_DroneBatteryLevels_Devices_Serial",
                table: "DroneBatteryLevels",
                column: "Serial",
                principalTable: "Devices",
                principalColumn: "SerialNumber",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DroneFlyingStates_Devices_Serial",
                table: "DroneFlyingStates",
                column: "Serial",
                principalTable: "Devices",
                principalColumn: "SerialNumber",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DroneRadioSignals_Devices_Serial",
                table: "DroneRadioSignals",
                column: "Serial",
                principalTable: "Devices",
                principalColumn: "SerialNumber",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DroneReplies_Devices_Serial",
                table: "DroneReplies",
                column: "Serial",
                principalTable: "Devices",
                principalColumn: "SerialNumber",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DroneVideoStatesResults_Devices_Serial",
                table: "DroneVideoStatesResults",
                column: "Serial",
                principalTable: "Devices",
                principalColumn: "SerialNumber",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Positions_Devices_Serial",
                table: "Positions",
                column: "Serial",
                principalTable: "Devices",
                principalColumn: "SerialNumber",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
