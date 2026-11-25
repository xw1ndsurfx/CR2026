using Intersect.Server.Database.PlayerData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.MySql.Player;
[DbContext(typeof(MySqlPlayerContext))]
[Migration("20261125010000_CraftingOrdersLedger")]
public sealed class CraftingOrdersLedgerMigration : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.CreateTable("CraftingOrdersLedger",
        columns: table => new { Id = table.Column<int>("int", nullable: false),
            State = table.Column<string>("longtext", nullable: false), Revision = table.Column<long>("bigint", nullable: false) },
        constraints: table => table.PrimaryKey("PK_CraftingOrdersLedger", x => x.Id));
    protected override void Down(MigrationBuilder builder) => builder.DropTable("CraftingOrdersLedger");
}
