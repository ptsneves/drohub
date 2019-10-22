using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class DropboxUsernameAndPasswordRemovedFromDeviceModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.DropColumn(
                    name: "DropboxPassword",
                    table: "Devices");

                migrationBuilder.DropColumn(
                name: "DropboxUsername",
                table: "Devices");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DropboxPassword",
                table: "Devices",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropboxUsername",
                table: "Devices",
                nullable: true);
        }
    }
}
