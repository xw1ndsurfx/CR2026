using Intersect.Server.Database.PlayerData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.Sqlite.Player;
[DbContext(typeof(SqlitePlayerContext))]
[Migration("20261020040000_PlayerNavalFleet")]
public sealed class PlayerNavalFleet : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.AddColumn<string>(name: "NavalFleet", table: "Players", type: "TEXT", nullable: true);
    protected override void Down(MigrationBuilder builder) => builder.DropColumn(name: "NavalFleet", table: "Players");
}
