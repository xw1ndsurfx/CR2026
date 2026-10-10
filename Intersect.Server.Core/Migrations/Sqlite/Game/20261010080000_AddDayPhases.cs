using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.Sqlite.Game;

/// <summary>Persist configurable sunrise/day/sunset/night transitions for the Time Editor.</summary>
[DbContext(typeof(SqliteGameContext))]
[Migration("20261010080000_AddDayPhases")]
public partial class AddDayPhases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DayPhases",
            table: "Time",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DayPhases", table: "Time");
    }
}
