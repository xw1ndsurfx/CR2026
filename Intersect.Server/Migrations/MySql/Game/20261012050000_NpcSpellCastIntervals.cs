using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.MySql.Game;

[DbContext(typeof(MySqlGameContext))]
[Migration("20261012050000_NpcSpellCastIntervals")]
public partial class NpcSpellCastIntervals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SpellCastIntervalsMs",
            table: "Npcs",
            type: "longtext",
            nullable: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SpellCastIntervalsMs", table: "Npcs");
    }
}
