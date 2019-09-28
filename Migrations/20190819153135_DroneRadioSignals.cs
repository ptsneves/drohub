using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class DroneRadioSignals : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
            name: "DroneRadioSignals",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                SignalQuality = table.Column<float>(nullable: true),
                RSSI = table.Column<float>(nullable: true),
                Serial = table.Column<string>(nullable: false),
                Timestamp = table.Column<long>(nullable: false)
            },

            constraints: table =>
            {
                table.PrimaryKey("PK_DroneRadioSignals", x => x.Id);
                table.ForeignKey(
                    name: "FK_DroneRadioSignals_Devices_Serial",
                    column: x => x.Serial,
                    principalTable: "Devices",
                    principalColumn: "SerialNumber",
                    onDelete: ReferentialAction.Cascade);
            });

            migrationBuilder.CreateIndex(
            name: "IX_DroneRadioSignals_Serial",
            table: "DroneRadioSignals",
            column: "Serial");

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DroneRadioSignals");
        }
    }
}
