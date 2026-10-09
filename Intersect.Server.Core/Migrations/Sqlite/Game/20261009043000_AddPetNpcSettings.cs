using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.Sqlite.Game;

[DbContext(typeof(SqliteGameContext))]
[Migration("20261009043000_AddPetNpcSettings")]
public partial class AddPetNpcSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("IsPet", "Npcs", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<Guid>("PetSummonItemId", "Npcs", type: "TEXT", nullable: false, defaultValue: Guid.Empty);
        migrationBuilder.AddColumn<int>("PetLootRadius", "Npcs", nullable: false, defaultValue: 2);
        migrationBuilder.AddColumn<int>("PetMaxLevel", "Npcs", nullable: false, defaultValue: 50);
        migrationBuilder.AddColumn<int>("PetStatGrowth", "Npcs", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<int>("PetHealthGrowth", "Npcs", nullable: false, defaultValue: 10);
        migrationBuilder.AddColumn<int>("PetSpellUnlockInterval", "Npcs", nullable: false, defaultValue: 5);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("IsPet", "Npcs");
        migrationBuilder.DropColumn("PetSummonItemId", "Npcs");
        migrationBuilder.DropColumn("PetLootRadius", "Npcs");
        migrationBuilder.DropColumn("PetMaxLevel", "Npcs");
        migrationBuilder.DropColumn("PetStatGrowth", "Npcs");
        migrationBuilder.DropColumn("PetHealthGrowth", "Npcs");
        migrationBuilder.DropColumn("PetSpellUnlockInterval", "Npcs");
    }
}
