using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.MySql.Game;

[DbContext(typeof(MySqlGameContext))]
[Migration("20261019133000_ShopBuybackDiscount")]
public sealed class ShopBuybackDiscount : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.AddColumn<int>(name: "BuybackDiscountPercent", table: "Shops", nullable: false, defaultValue: 0);
    protected override void Down(MigrationBuilder builder) => builder.DropColumn(name: "BuybackDiscountPercent", table: "Shops");
}
