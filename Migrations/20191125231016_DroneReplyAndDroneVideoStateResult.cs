using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class DroneReplyAndDroneVideoStateResult : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DroneReplies",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActionName = table.Column<string>(nullable: true),
                    Result = table.Column<bool>(nullable: false),
                    Serial = table.Column<string>(nullable: false),
                    Timestamp = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DroneReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DroneReplies_Devices_Serial",
                        column: x => x.Serial,
                        principalTable: "Devices",
                        principalColumn: "SerialNumber",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DroneVideoStatesResults",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    State = table.Column<int>(nullable: false),
                    HumanStruct = table.Column<string>(nullable: true),
                    RtpUrl = table.Column<string>(nullable: true),
                    Serial = table.Column<string>(nullable: false),
                    Timestamp = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DroneVideoStatesResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DroneVideoStatesResults_Devices_Serial",
                        column: x => x.Serial,
                        principalTable: "Devices",
                        principalColumn: "SerialNumber",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DroneReplies_Serial",
                table: "DroneReplies",
                column: "Serial");

            migrationBuilder.CreateIndex(
                name: "IX_DroneVideoStatesResults_Serial",
                table: "DroneVideoStatesResults",
                column: "Serial");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DroneReplies");

            migrationBuilder.DropTable(
                name: "DroneVideoStatesResults");
        }
    }
}
