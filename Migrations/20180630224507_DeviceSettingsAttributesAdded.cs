using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class DeviceSettingsAttributesAdded : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ID",
                table: "Devices",
                newName: "Id");

            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "Devices",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Apperture",
                table: "Devices",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FocusMode",
                table: "Devices",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ISO",
                table: "Devices",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices",
                column: "SerialNumber",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Apperture",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "FocusMode",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ISO",
                table: "Devices");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Devices",
                newName: "ID");

            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "Devices",
                nullable: true,
                oldClrType: typeof(string));
        }
    }
}
