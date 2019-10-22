using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class DeviceLiveVideoData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<string>(
                name: "LiveVideoSecret",
                table: "Devices",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
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

                migrationBuilder.DropColumn(
                    name: "LiveVideoSecret",
                    table: "Devices");
            }
        }
    }
}
