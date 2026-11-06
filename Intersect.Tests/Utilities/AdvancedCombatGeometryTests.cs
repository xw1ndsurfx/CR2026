using System.Linq;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.NPCs;
using NUnit.Framework;

namespace Intersect.Utilities;

[TestFixture]
public class AdvancedCombatGeometryTests
{
    [Test]
    public void DefaultTargetTileKeepsLegacyBossBehaviour()
    {
        var tiles = CombatTelegraphGeometry.GetTiles(
            CombatTelegraphShape.TargetTile, 4, 5, 4, 4, Direction.Up, 1);
        Assert.That(tiles, Is.EquivalentTo(new[] { new CombatTelegraphTile(4, 4) }));
    }

    [Test]
    public void LineUsesFacingAndClampsAuthorRange()
    {
        var tiles = CombatTelegraphGeometry.GetTiles(
            CombatTelegraphShape.Line, 5, 5, 5, 4, Direction.Up, 100);
        Assert.That(tiles, Has.Count.EqualTo(CombatTelegraphGeometry.MaximumRange));
        Assert.That(tiles, Does.Contain(new CombatTelegraphTile(5, 1)));
        Assert.That(tiles, Does.Not.Contain(new CombatTelegraphTile(6, 4)));
    }

    [Test]
    public void ConeDoesNotStrikeBehindMonster()
    {
        var tiles = CombatTelegraphGeometry.GetTiles(
            CombatTelegraphShape.Cone, 5, 5, 5, 4, Direction.Up, 2);
        Assert.That(tiles, Does.Contain(new CombatTelegraphTile(5, 4)));
        Assert.That(tiles, Does.Contain(new CombatTelegraphTile(4, 3)));
        Assert.That(tiles, Does.Not.Contain(new CombatTelegraphTile(5, 6)));
        Assert.That(tiles.Distinct().Count(), Is.EqualTo(tiles.Count));
    }

    [Test]
    public void CircleUsesTargetTileAndEuclideanRadius()
    {
        var tiles = CombatTelegraphGeometry.GetTiles(
            CombatTelegraphShape.Circle, 0, 0, 6, 6, Direction.Down, 2);
        Assert.That(tiles, Does.Contain(new CombatTelegraphTile(6, 6)));
        Assert.That(tiles, Does.Contain(new CombatTelegraphTile(8, 6)));
        Assert.That(tiles, Does.Not.Contain(new CombatTelegraphTile(8, 8)));
    }

    [Test]
    public void InvalidShapeSafelyFallsBackToOneTile()
    {
        var tiles = CombatTelegraphGeometry.GetTiles(
            (CombatTelegraphShape)999, 4, 5, 4, 4, Direction.Up, 2);
        Assert.That(tiles, Is.EquivalentTo(new[] { new CombatTelegraphTile(4, 4) }));
    }
}
