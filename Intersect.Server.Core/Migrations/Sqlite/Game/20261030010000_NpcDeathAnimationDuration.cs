using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.Sqlite.Game;

[DbContext(typeof(SqliteGameContext))]
[Migration("20261030010000_NpcDeathAnimationDuration")]
public partial class NpcDeathAnimationDuration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<int>(
        name: "DeathAnimationDurationSeconds", table: "Npcs", nullable: false, defaultValue: 0);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("DeathAnimationDurationSeconds", "Npcs");
}
