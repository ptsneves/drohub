using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using System;

namespace DroHub.Data.Migrations
{
    public partial class MySQLIncrementingID : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET foreign_key_checks = 0");
            var pomeloMigrationNoteCore2Bug = "ALTER TABLE {0} MODIFY COLUMN {1} int auto_increment";

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Devices",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);
            migrationBuilder.Sql(String.Format(pomeloMigrationNoteCore2Bug, "Devices", "Id"));
            migrationBuilder.Sql("SET foreign_key_checks = 1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //I do not know how to revert this
        }
    }
}
