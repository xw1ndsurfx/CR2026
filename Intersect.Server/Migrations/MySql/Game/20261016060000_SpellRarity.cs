using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.MySql.Game;
[DbContext(typeof(MySqlGameContext))]
[Migration("20261016060000_SpellRarity")]
public sealed class SpellRarity : Migration
{
    protected override void Up(MigrationBuilder builder) =>
        builder.AddColumn<int>(name: "Rarity", table: "Spells", nullable: false, defaultValue: 0);
    protected override void Down(MigrationBuilder builder) => builder.DropColumn(name: "Rarity", table: "Spells");
}
