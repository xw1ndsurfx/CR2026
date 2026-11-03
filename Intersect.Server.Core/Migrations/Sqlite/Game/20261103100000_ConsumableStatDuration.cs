using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.Sqlite.Game;
[DbContext(typeof(SqliteGameContext))]
[Migration("20261103100000_ConsumableStatDuration")]
public partial class ConsumableStatDuration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<int>("ConsumableStatDurationSeconds", "Items", nullable: false, defaultValue: 0);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn("ConsumableStatDurationSeconds", "Items");
}
