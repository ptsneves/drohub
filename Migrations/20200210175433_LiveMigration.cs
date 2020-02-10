using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class LiveMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HumanMessage",
                table: "DroneVideoStatesResults");

            migrationBuilder.DropColumn(
                name: "RtpUrl",
                table: "DroneVideoStatesResults");

            migrationBuilder.DropColumn(
                name: "LiveVideoFMTProfile",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LiveVideoPt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LiveVideoRTPMap",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LiveVideoRTPUrl",
                table: "Devices");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HumanMessage",
                table: "DroneVideoStatesResults",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RtpUrl",
                table: "DroneVideoStatesResults",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiveVideoFMTProfile",
                table: "Devices",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LiveVideoPt",
                table: "Devices",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LiveVideoRTPMap",
                table: "Devices",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiveVideoRTPUrl",
                table: "Devices",
                nullable: true);
        }
    }
}
