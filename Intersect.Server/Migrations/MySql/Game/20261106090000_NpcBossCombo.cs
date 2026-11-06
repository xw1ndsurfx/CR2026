using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.MySql.Game;

[DbContext(typeof(MySqlGameContext))]
[Migration("20261106090000_NpcBossCombo")]
public partial class NpcBossCombo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "AdvancedBossComboEnabled", table: "Npcs",
            nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>(
            name: "AdvancedBossComboCount", table: "Npcs",
            nullable: false, defaultValue: 2);
        migrationBuilder.AddColumn<int>(
            name: "AdvancedBossComboSecondShape", table: "Npcs",
            nullable: false, defaultValue: 2);
        migrationBuilder.AddColumn<int>(
            name: "AdvancedBossComboThirdShape", table: "Npcs",
            nullable: false, defaultValue: 3);
        migrationBuilder.AddColumn<int>(
            name: "AdvancedBossComboFollowupWindupMs", table: "Npcs",
            nullable: false, defaultValue: 550);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AdvancedBossComboEnabled", table: "Npcs");
        migrationBuilder.DropColumn(name: "AdvancedBossComboCount", table: "Npcs");
        migrationBuilder.DropColumn(name: "AdvancedBossComboSecondShape", table: "Npcs");
        migrationBuilder.DropColumn(name: "AdvancedBossComboThirdShape", table: "Npcs");
        migrationBuilder.DropColumn(name: "AdvancedBossComboFollowupWindupMs", table: "Npcs");
    }
}
