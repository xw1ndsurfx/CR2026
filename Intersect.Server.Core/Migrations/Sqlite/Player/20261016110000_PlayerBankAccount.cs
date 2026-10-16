using Intersect.Server.Database.PlayerData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.Sqlite.Player;
[DbContext(typeof(SqlitePlayerContext))]
[Migration("20261016110000_PlayerBankAccount")]
public sealed class PlayerBankAccount : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.AddColumn<string>(name:"BankAccount",table:"Players",type:"TEXT",nullable:true);
    protected override void Down(MigrationBuilder builder) => builder.DropColumn(name:"BankAccount",table:"Players");
}
