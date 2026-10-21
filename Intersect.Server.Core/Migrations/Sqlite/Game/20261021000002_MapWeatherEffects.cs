using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.Sqlite.Game;
[DbContext(typeof(SqliteGameContext))]
[Migration("20261021000002_MapWeatherEffects")]
public partial class MapWeatherEffects : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.AddColumn<string>(name:"WeatherEffects",table:"Maps",nullable:false,defaultValue:"{}");
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.DropColumn(name:"WeatherEffects",table:"Maps");
}
