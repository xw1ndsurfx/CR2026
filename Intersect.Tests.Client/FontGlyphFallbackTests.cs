using Intersect.Client.MonoGame.Graphics;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class FontGlyphFallbackTests
{
    private static readonly char[] Ascii = Enumerable.Range(32, 95).Select(value => (char)value).ToArray();

    [TestCase("Fish Blue Devil Female ×1", "Fish Blue Devil Female x1")]
    [TestCase("‹ × ›", "< x >")]
    [TestCase("Waiting…", "Waiting...")]
    [TestCase("23\u202f031\u00a032", "23 031 32")]
    [TestCase("Tension · Depth — Auto → Bank", "Tension . Depth - Auto -> Bank")]
    [TestCase("‘Loot’ \"Rare\"", "'Loot' \"Rare\"")]
    public void MissingTypographyUsesDisplayableEquivalents(string source, string expected)
    {
        var fallback = new FontGlyphFallback(Ascii, '?');
        Assert.That(fallback.Prepare(source), Is.EqualTo(expected));
        Assert.That(source, Is.Not.EqualTo(expected), "Only the display string is converted.");
    }

    [Test]
    public void AvailableGlyphsAndFrenchSpanishAccentsArePreserved()
    {
        const string text = "Pêche ×1 · Équipement — Caña ¿sí?…";
        var fallback = new FontGlyphFallback(Ascii.Concat(text), '?');
        Assert.That(fallback.Prepare(text), Is.SameAs(text));
    }

    [Test]
    public void LineBreaksAndActualQuestionMarksRemainIntact()
    {
        var fallback = new FontGlyphFallback(Ascii, '?');
        Assert.That(fallback.Prepare("Fish?\r\nLoot ×2\tOK"), Is.EqualTo("Fish?\r\nLoot x2    OK"));
    }

    [Test]
    public void FallbackCannotIntroduceAnotherMissingGlyph()
    {
        var fallback = new FontGlyphFallback(['?', ' '], '?');
        Assert.That(fallback.Prepare("×…‹"), Is.EqualTo("???"));
        Assert.That(fallback.Prepare("☃"), Is.EqualTo("?"));
    }
}
