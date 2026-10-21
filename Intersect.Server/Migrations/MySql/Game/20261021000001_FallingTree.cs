using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.MySql.Game;
[DbContext(typeof(MySqlGameContext))]
[Migration("20261021000001_FallingTree")]
public partial class FallingTree : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.AddColumn<bool>(name:"FallingTree",table:"Resources",nullable:false,defaultValue:false);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.DropColumn(name:"FallingTree",table:"Resources");
}
