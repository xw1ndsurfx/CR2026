using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Intersect.Server.Migrations.MySql.Game;

[DbContext(typeof(MySqlGameContext))]
[Migration("20261001030000_AnimationLayerScale")]
public partial class AnimationLayerScale : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Lower_ScalePercent",
            table: "Animations",
            nullable: false,
            defaultValue: 100
        );

        migrationBuilder.AddColumn<int>(
            name: "Upper_ScalePercent",
            table: "Animations",
            nullable: false,
            defaultValue: 100
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Lower_ScalePercent",
            table: "Animations"
        );

        migrationBuilder.DropColumn(
            name: "Upper_ScalePercent",
            table: "Animations"
        );
    }
}
