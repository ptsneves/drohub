using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class AddCameraState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CameraStates",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ConnectionId = table.Column<long>(nullable: false),
                    Mode = table.Column<int>(nullable: false),
                    ZoomLevel = table.Column<double>(nullable: false),
                    MinZoom = table.Column<double>(nullable: false),
                    MaxZoom = table.Column<double>(nullable: false),
                    Serial = table.Column<string>(nullable: true),
                    Timestamp = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CameraStates_DeviceConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "DeviceConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CameraStates_ConnectionId",
                table: "CameraStates",
                column: "ConnectionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CameraStates");
        }
    }
}
