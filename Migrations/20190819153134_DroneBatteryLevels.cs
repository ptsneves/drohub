using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class DroneBatteryLevels : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DroneBatteryLevels",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    BatteryLevelPercent = table.Column<double>(nullable: false),
                    Serial = table.Column<string>(nullable: false),
                    Timestamp = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DroneBatteryLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DroneBatteryLevels_Devices_Serial",
                        column: x => x.Serial,
                        principalTable: "Devices",
                        principalColumn: "SerialNumber",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DroneBatteryLevels_Serial",
                table: "DroneBatteryLevels",
                column: "Serial");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DroneBatteryLevels");
        }
    }
}
