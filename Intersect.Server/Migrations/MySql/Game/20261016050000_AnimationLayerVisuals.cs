using Intersect.Server.Database.GameData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Intersect.Server.Migrations.MySql.Game;
[DbContext(typeof(MySqlGameContext))]
[Migration("20261016050000_AnimationLayerVisuals")]
public sealed class AnimationLayerVisuals : Migration
{
    protected override void Up(MigrationBuilder builder)
    {
        foreach (var layer in new[] { "Lower", "Upper" })
            foreach (var property in new[] { "OffsetX", "OffsetY", "TransparencyPercent" })
                builder.AddColumn<int>(name: layer + "_" + property, table: "Animations", nullable: false, defaultValue: 0);
    }
    protected override void Down(MigrationBuilder builder)
    {
        foreach (var layer in new[] { "Lower", "Upper" })
            foreach (var property in new[] { "OffsetX", "OffsetY", "TransparencyPercent" })
                builder.DropColumn(name: layer + "_" + property, table: "Animations");
    }
}
