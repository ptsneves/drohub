using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class DropboxCredentialsOnDevices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DropboxConnectState",
                table: "Devices",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropboxPassword",
                table: "Devices",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropboxToken",
                table: "Devices",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropboxUsername",
                table: "Devices",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DropboxConnectState",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DropboxPassword",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DropboxToken",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DropboxUsername",
                table: "Devices");
        }
    }
}
