using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class MediaObjectsAndTags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaObjects",
                columns: table => new
                {
                    MediaPath = table.Column<string>(nullable: false),
                    SubscriptionOrganizationName = table.Column<string>(nullable: true),
                    DeviceConnectionId = table.Column<long>(nullable: false),
                    CaptureDateTimeUTC = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaObjects", x => x.MediaPath);
                    table.ForeignKey(
                        name: "FK_MediaObjects_DeviceConnections_DeviceConnectionId",
                        column: x => x.DeviceConnectionId,
                        principalTable: "DeviceConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaObjects_Subscriptions_SubscriptionOrganizationName",
                        column: x => x.SubscriptionOrganizationName,
                        principalTable: "Subscriptions",
                        principalColumn: "OrganizationName",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaObjectTags",
                columns: table => new
                {
                    MediaPath = table.Column<string>(nullable: false),
                    TagName = table.Column<string>(nullable: false),
                    Timestamp = table.Column<DateTime>(nullable: true),
                    SubscriptionOrganizationName = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaObjectTags", x => new { x.TagName, x.MediaPath });
                    table.ForeignKey(
                        name: "FK_MediaObjectTags_MediaObjects_MediaPath",
                        column: x => x.MediaPath,
                        principalTable: "MediaObjects",
                        principalColumn: "MediaPath",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaObjectTags_Subscriptions_SubscriptionOrganizationName",
                        column: x => x.SubscriptionOrganizationName,
                        principalTable: "Subscriptions",
                        principalColumn: "OrganizationName",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaObjects_DeviceConnectionId",
                table: "MediaObjects",
                column: "DeviceConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaObjects_SubscriptionOrganizationName",
                table: "MediaObjects",
                column: "SubscriptionOrganizationName");

            migrationBuilder.CreateIndex(
                name: "IX_MediaObjectTags_MediaPath",
                table: "MediaObjectTags",
                column: "MediaPath");

            migrationBuilder.CreateIndex(
                name: "IX_MediaObjectTags_SubscriptionOrganizationName",
                table: "MediaObjectTags",
                column: "SubscriptionOrganizationName");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaObjectTags");

            migrationBuilder.DropTable(
                name: "MediaObjects");
        }
    }
}
