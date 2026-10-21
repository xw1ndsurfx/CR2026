using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.Sqlite.Game;
[DbContext(typeof(SqliteGameContext))]
[Migration("20261021000000_ItemPresentation")]
public partial class ItemPresentation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.AddColumn<string>(name:"Presentation",table:"Items",nullable:false,defaultValue:"{}");
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.DropColumn(name:"Presentation",table:"Items");
}
