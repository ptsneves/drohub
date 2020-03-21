using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DroHub.Data.Migrations
{
    public partial class SubscriptionTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubscriptionOrganizationName",
                table: "AspNetUsers",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    OrganizationName = table.Column<string>(nullable: false),
                    AllowedUserCount = table.Column<int>(nullable: false),
                    AllowedFlightTime = table.Column<TimeSpan>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.OrganizationName);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SubscriptionOrganizationName",
                table: "AspNetUsers",
                column: "SubscriptionOrganizationName");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Subscriptions_SubscriptionOrganizationName",
                table: "AspNetUsers",
                column: "SubscriptionOrganizationName",
                principalTable: "Subscriptions",
                principalColumn: "OrganizationName",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Subscriptions_SubscriptionOrganizationName",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SubscriptionOrganizationName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SubscriptionOrganizationName",
                table: "AspNetUsers");
        }
    }
}
