using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.MySql.Game;

[DbContext(typeof(MySqlGameContext))]
[Migration("20261109010000_NpcSoundAmbience")]
public partial class NpcSoundAmbience : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "DeathSound", table: "Npcs", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "ProximitySound", table: "Npcs", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<int>(name: "ProximitySoundRadius", table: "Npcs", nullable: false, defaultValue: 5);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DeathSound", table: "Npcs");
        migrationBuilder.DropColumn(name: "ProximitySound", table: "Npcs");
        migrationBuilder.DropColumn(name: "ProximitySoundRadius", table: "Npcs");
    }
}
