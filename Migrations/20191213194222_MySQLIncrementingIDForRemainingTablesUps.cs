using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using System;

namespace DroHub.Data.Migrations
{
    public partial class MySQLIncrementingIDForRemainingTablesUps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var pomeloMigrationNoteCore2Bug = "ALTER TABLE {0} MODIFY COLUMN {1} int auto_increment";
            string[] tables = { "Positions", "DroneVideoStatesResults", "DroneReplies", "DroneRadioSignals", "DroneFlyingStates", "DroneBatteryLevels" };
            migrationBuilder.Sql("SET foreign_key_checks = 0");
            foreach(var table in tables) {
                migrationBuilder.AlterColumn<int>(
                    name: "Id",
                    table: table,
                    nullable: false,
                    oldClrType: typeof(int))
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);
                migrationBuilder.Sql(String.Format(pomeloMigrationNoteCore2Bug, table, "Id"));
            }
            migrationBuilder.Sql("SET foreign_key_checks = 1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
