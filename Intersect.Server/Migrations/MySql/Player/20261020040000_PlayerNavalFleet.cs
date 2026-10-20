using Intersect.Server.Database.PlayerData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.MySql.Player;
[DbContext(typeof(MySqlPlayerContext))]
[Migration("20261020040000_PlayerNavalFleet")]
public sealed class PlayerNavalFleet : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.AddColumn<string>(name: "NavalFleet", table: "Players", type: "longtext", nullable: true);
    protected override void Down(MigrationBuilder builder) => builder.DropColumn(name: "NavalFleet", table: "Players");
}
