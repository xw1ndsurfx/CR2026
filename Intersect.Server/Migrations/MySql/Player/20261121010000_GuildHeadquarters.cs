using Intersect.Server.Database.PlayerData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.MySql.Player;
[DbContext(typeof(MySqlPlayerContext))]
[Migration("20261121010000_GuildHeadquarters")]
public sealed class GuildHeadquarters : Migration
{
    protected override void Up(MigrationBuilder builder)
    {
        builder.AddColumn<string>(name: "Headquarters", table: "Guilds", type: "longtext", nullable: true);
        builder.AddColumn<string>(name: "GuildHeadquartersVisit", table: "Players", type: "longtext", nullable: true);
    }
    protected override void Down(MigrationBuilder builder)
    {
        builder.DropColumn(name: "Headquarters", table: "Guilds");
        builder.DropColumn(name: "GuildHeadquartersVisit", table: "Players");
    }
}
