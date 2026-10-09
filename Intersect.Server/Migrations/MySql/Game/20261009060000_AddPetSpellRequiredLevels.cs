using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.MySql.Game;

/// <summary>
/// The companion-only level mapping is nullable for existing NPC records.
/// </summary>
[DbContext(typeof(MySqlGameContext))]
[Migration("20261009060000_AddPetSpellRequiredLevels")]
public partial class AddPetSpellRequiredLevels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PetSpellRequiredLevels",
            table: "Npcs",
            type: "longtext",
            nullable: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PetSpellRequiredLevels",
            table: "Npcs"
        );
    }
}
