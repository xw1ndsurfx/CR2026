using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.Sqlite.Game;

[DbContext(typeof(SqliteGameContext))]
[Migration("20261001010000_NpcBossSettings")]
public partial class NpcBossSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsBoss",
            table: "Npcs",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<Guid>(
            name: "BossAnimation",
            table: "Npcs",
            nullable: false,
            defaultValue: Guid.Empty
        );

        migrationBuilder.AddColumn<int>(
            name: "BossAnimationOffsetY",
            table: "Npcs",
            nullable: false,
            defaultValue: -48
        );

        migrationBuilder.AddColumn<Guid>(
            name: "DeathAnimation",
            table: "Npcs",
            nullable: false,
            defaultValue: Guid.Empty
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsBoss", table: "Npcs");
        migrationBuilder.DropColumn(name: "BossAnimation", table: "Npcs");
        migrationBuilder.DropColumn(name: "BossAnimationOffsetY", table: "Npcs");
        migrationBuilder.DropColumn(name: "DeathAnimation", table: "Npcs");
    }
}
