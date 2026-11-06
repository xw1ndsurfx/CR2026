using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.Sqlite.Game;

[DbContext(typeof(SqliteGameContext))]
[Migration("20261106100000_NpcAdvancedSpellTelegraph")]
public partial class NpcAdvancedSpellTelegraph : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "AdvancedSpellTelegraphEnabled", table: "Npcs",
            nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>(
            name: "AdvancedSpellTelegraphWindupMs", table: "Npcs",
            nullable: false, defaultValue: 650);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AdvancedSpellTelegraphEnabled", table: "Npcs");
        migrationBuilder.DropColumn(name: "AdvancedSpellTelegraphWindupMs", table: "Npcs");
    }
}
