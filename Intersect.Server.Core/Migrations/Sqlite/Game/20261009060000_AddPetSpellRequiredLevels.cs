using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.Sqlite.Game;

/// <summary>
/// Pet spell requirements are stored separately from NPC Spells. Existing
/// NPCs and companions keep their current spell lists and cast behavior.
/// </summary>
[DbContext(typeof(SqliteGameContext))]
[Migration("20261009060000_AddPetSpellRequiredLevels")]
public partial class AddPetSpellRequiredLevels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PetSpellRequiredLevels",
            table: "Npcs",
            type: "TEXT",
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
