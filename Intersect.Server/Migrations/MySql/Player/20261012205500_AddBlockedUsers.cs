using Intersect.Server.Database.PlayerData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intersect.Server.Migrations.MySql.Player;

[DbContext(typeof(MySqlPlayerContext))]
[Migration("20261012205500_AddBlockedUsers")]
public partial class AddBlockedUsers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // MySQL 5.7 and some 8.0 variants reject DEFAULT values for
        // LONGTEXT columns. Add as nullable, populate all existing players,
        // then enforce NOT NULL without declaring a database default.
        migrationBuilder.AddColumn<string>(
            name: "BlockedUsers",
            table: "Players",
            type: "longtext",
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE `Players` SET `BlockedUsers` = '{}' WHERE `BlockedUsers` IS NULL");

        migrationBuilder.AlterColumn<string>(
            name: "BlockedUsers",
            table: "Players",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "BlockedUsers", table: "Players");
    }
}
