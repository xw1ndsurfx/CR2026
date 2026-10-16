using Intersect.Server.Database.PlayerData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.MySql.Player;
[DbContext(typeof(MySqlPlayerContext))]
[Migration("20261016070000_PlayerHousing")]
public sealed class PlayerHousing : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.AddColumn<string>(name: "Housing", table: "Players", type: "longtext", nullable: true);
    protected override void Down(MigrationBuilder builder) => builder.DropColumn(name: "Housing", table: "Players");
}
