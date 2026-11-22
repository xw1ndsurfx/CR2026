using Intersect.Server.Database.PlayerData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.Sqlite.Player;
[DbContext(typeof(SqlitePlayerContext))]
[Migration("20261121010000_GuildHeadquarters")]
public sealed class GuildHeadquarters : Migration
{
    protected override void Up(MigrationBuilder builder)
    {
        builder.AddColumn<string>(name: "Headquarters", table: "Guilds", type: "TEXT", nullable: true);
        builder.AddColumn<string>(name: "GuildHeadquartersVisit", table: "Players", type: "TEXT", nullable: true);
    }
    protected override void Down(MigrationBuilder builder)
    {
        builder.DropColumn(name: "Headquarters", table: "Guilds");
        builder.DropColumn(name: "GuildHeadquartersVisit", table: "Players");
    }
}
