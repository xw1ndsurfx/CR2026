using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.Sqlite.Game;

[DbContext(typeof(SqliteGameContext))]
[Migration("20261019120000_NpcExperienceRange")]
public sealed class NpcExperienceRange : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.AddColumn<long>(name: "ExperienceMaximum", table: "Npcs", nullable: false, defaultValue: 0L);
    protected override void Down(MigrationBuilder builder) => builder.DropColumn(name: "ExperienceMaximum", table: "Npcs");
}
