using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.MySql.Game;

[DbContext(typeof(MySqlGameContext))]
[Migration("20261106040000_NpcAdvancedTelegraphs")]
public partial class NpcAdvancedTelegraphs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "AdvancedTelegraphEnabled", table: "Npcs",
            nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>(
            name: "AdvancedTelegraphShape", table: "Npcs",
            nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(
            name: "AdvancedTelegraphRange", table: "Npcs",
            nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<int>(
            name: "AdvancedTelegraphWindupMs", table: "Npcs",
            nullable: false, defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AdvancedTelegraphEnabled", table: "Npcs");
        migrationBuilder.DropColumn(name: "AdvancedTelegraphShape", table: "Npcs");
        migrationBuilder.DropColumn(name: "AdvancedTelegraphRange", table: "Npcs");
        migrationBuilder.DropColumn(name: "AdvancedTelegraphWindupMs", table: "Npcs");
    }
}
