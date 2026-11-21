using Intersect.Client.MiniGames;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class PotionPresentationLayoutTests
{
    [TestCase(640, 480)]
    [TestCase(1280, 720)]
    [TestCase(1920, 1080)]
    [TestCase(2560, 1080)]
    public void AlchemyUsesTheWholeViewportAndKeepsActionsInsideIt(int width, int height)
    {
        var layout = new PotionSceneLayout(width, height);
        Assert.That(layout.Rect(0, 0, 1000, 780), Is.EqualTo(new PokerSceneRect(0, 0, width, height)));
        var recipe = layout.Rect(45, 90, 355, 675);
        var board = layout.Rect(430, 140, 416, 480);
        var exit = layout.Rect(275, 730, 105, 32);
        Assert.That(recipe.X + recipe.Width, Is.LessThan(board.X));
        Assert.That(board.X + board.Width, Is.LessThanOrEqualTo(width));
        Assert.That(exit.Y + exit.Height, Is.LessThanOrEqualTo(height));
    }

    [Test]
    public void TransparentGuttersDoNotShrinkOrShiftIngredients()
    {
        const int width = 120, height = 100;
        var alpha = new byte[width * height];
        for (var y = 8; y < 18; y++)
        for (var x = 17; x < 23; x++) alpha[y * width + x] = 255;
        alpha[0] = 12; // faint outer speckle must not change the visible bounds
        for (var y = 10; y < 14; y++) alpha[y * width + 29] = 255; // neighboring sprite fragment on the right cell edge
        var cells = PotionAtlasLayout.FindCells(width, height, alpha);
        Assert.That(cells[0], Is.EqualTo(new PotionAtlasCell(17, 8, 6, 10)));
        var fitted = PotionAtlasLayout.Fit(cells[0], new PokerSceneRect(100, 200, 60, 48), 1);
        Assert.That(fitted.Height, Is.InRange(39, 41), "The 10-pixel sprite must fill most of the 48-pixel board cell.");
        Assert.That(fitted.Width / (double)fitted.Height, Is.EqualTo(.6).Within(.04));
        Assert.That(fitted.X + fitted.Width / 2.0, Is.EqualTo(130).Within(1));
        Assert.That(fitted.Y + fitted.Height / 2.0, Is.EqualTo(224).Within(1));
        Assert.That(cells[1], Is.EqualTo(new PotionAtlasCell(30, 0, 30, 25)), "An empty cell safely keeps its normal UV bounds.");
    }

    [Test]
    public void CustomAtlasesWithoutReadablePixelsRetainTheFourByFourContract()
    {
        var cells = PotionAtlasLayout.FindCells(1261, 1247, null);
        Assert.That(cells.Length, Is.EqualTo(16));
        var last = cells[15];
        Assert.That(last.X + last.Width, Is.EqualTo(1261));
        Assert.That(last.Y + last.Height, Is.EqualTo(1247));
        var target = new PokerSceneRect(0, 0, 52, 48);
        var small = PotionAtlasLayout.Fit(last, target, 1);
        var large = PotionAtlasLayout.Fit(last, target, 4);
        Assert.That(large.Height, Is.GreaterThan(small.Height));
        Assert.That(large.X, Is.GreaterThanOrEqualTo(0));
        Assert.That(large.Y + large.Height, Is.LessThanOrEqualTo(target.Height));
    }
}
