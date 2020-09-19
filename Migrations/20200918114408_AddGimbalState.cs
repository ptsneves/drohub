using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class AddGimbalState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GimbalStates",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ConnectionId = table.Column<long>(nullable: false),
                    CalibrationState = table.Column<int>(nullable: false),
                    Roll = table.Column<double>(nullable: false),
                    Pitch = table.Column<double>(nullable: false),
                    Yaw = table.Column<double>(nullable: false),
                    MinRoll = table.Column<double>(nullable: false),
                    MaxRoll = table.Column<double>(nullable: false),
                    MinYaw = table.Column<double>(nullable: false),
                    MaxYaw = table.Column<double>(nullable: false),
                    MinPitch = table.Column<double>(nullable: false),
                    MaxPitch = table.Column<double>(nullable: false),
                    IsRollStastabilized = table.Column<bool>(nullable: false),
                    IsYawStabilized = table.Column<bool>(nullable: false),
                    IsPitchStabilized = table.Column<bool>(nullable: false),
                    Serial = table.Column<string>(nullable: true),
                    Timestamp = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GimbalStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GimbalStates_DeviceConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "DeviceConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GimbalStates_ConnectionId",
                table: "GimbalStates",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_GimbalStates_Serial",
                table: "GimbalStates",
                column: "Serial");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GimbalStates");
        }
    }
}
