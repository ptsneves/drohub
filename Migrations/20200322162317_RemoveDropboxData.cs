using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class RemoveDropboxData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DropboxConnectState",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DropboxToken",
                table: "Devices");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DropboxConnectState",
                table: "Devices",
                type: "varchar(32) CHARACTER SET utf8mb4",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropboxToken",
                table: "Devices",
                type: "longtext CHARACTER SET utf8mb4",
                nullable: true);
        }
    }
}
