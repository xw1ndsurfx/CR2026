using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.Sqlite.Game;

[DbContext(typeof(SqliteGameContext))]
[Migration("20261001020000_NpcCombatMovementAI")]
public partial class NpcCombatMovementAI : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "SmartCombatMovement",
            table: "Npcs",
            nullable: false,
            defaultValue: true
        );

        migrationBuilder.AddColumn<int>(
            name: "CombatMovementMode",
            table: "Npcs",
            nullable: false,
            defaultValue: 0
        );

        migrationBuilder.AddColumn<int>(
            name: "PreferredCombatRange",
            table: "Npcs",
            nullable: false,
            defaultValue: 0
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SmartCombatMovement", table: "Npcs");
        migrationBuilder.DropColumn(name: "CombatMovementMode", table: "Npcs");
        migrationBuilder.DropColumn(name: "PreferredCombatRange", table: "Npcs");
    }
}
