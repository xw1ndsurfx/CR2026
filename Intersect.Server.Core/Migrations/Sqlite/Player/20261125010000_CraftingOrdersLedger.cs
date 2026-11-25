using Intersect.Server.Database.PlayerData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.Sqlite.Player;
[DbContext(typeof(SqlitePlayerContext))]
[Migration("20261125010000_CraftingOrdersLedger")]
public sealed class CraftingOrdersLedgerMigration : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.CreateTable("CraftingOrdersLedger",
        columns: table => new { Id = table.Column<int>("INTEGER", nullable: false),
            State = table.Column<string>("TEXT", nullable: false), Revision = table.Column<long>("INTEGER", nullable: false) },
        constraints: table => table.PrimaryKey("PK_CraftingOrdersLedger", x => x.Id));
    protected override void Down(MigrationBuilder builder) => builder.DropTable("CraftingOrdersLedger");
}
