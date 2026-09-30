using System.Text.RegularExpressions;
using Intersect.Client.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics;
using XNARectangle = Microsoft.Xna.Framework.Rectangle;
using XNAVector3 = Microsoft.Xna.Framework.Vector3;

namespace Intersect.Client.MonoGame.Graphics;

/// <summary>
/// Runtime SpriteFont backed directly by a fixed-cell bitmap font sheet.
/// This avoids TTF rasterization so true pixel fonts keep their authored pixels.
/// </summary>
internal sealed class MonoBitmapFont : Font<SpriteFont>, IFont
{
    private readonly SpriteFont _spriteFont;

    public MonoBitmapFont(
        string fontName,
        string spriteSheetPath,
        string metricsPath,
        ICollection<int> supportedSizes,
        GraphicsDevice graphicsDevice
    ) : base(fontName, supportedSizes)
    {
        if (string.IsNullOrWhiteSpace(spriteSheetPath))
        {
            throw new ArgumentException("Bitmap font sprite-sheet path is required.", nameof(spriteSheetPath));
        }

        if (string.IsNullOrWhiteSpace(metricsPath))
        {
            throw new ArgumentException("Bitmap font metrics path is required.", nameof(metricsPath));
        }

        ArgumentNullException.ThrowIfNull(graphicsDevice);

        var metrics = ParseMetrics(metricsPath);

        using var stream = File.OpenRead(spriteSheetPath);
        var texture = Texture2D.FromStream(graphicsDevice, stream);
        texture.Name = $"{fontName} bitmap source";

        if (texture.Width % metrics.CellWidth != 0 || texture.Height % metrics.CellHeight != 0)
        {
            texture.Dispose();
            throw new InvalidDataException(
                $"Bitmap font '{fontName}' sheet dimensions {texture.Width}x{texture.Height} do not align " +
                $"to the declared {metrics.CellWidth}x{metrics.CellHeight} cells."
            );
        }

        var columns = texture.Width / metrics.CellWidth;
        var totalCells = columns * (texture.Height / metrics.CellHeight);
        if (metrics.CharacterSet.Length + 1 > totalCells)
        {
            texture.Dispose();
            throw new InvalidDataException(
                $"Bitmap font '{fontName}' needs {metrics.CharacterSet.Length + 1} cells but the sheet only has {totalCells}."
            );
        }

        var glyphEntries = new List<GlyphEntry>(metrics.CharacterSet.Length + 1);
        for (var sourceIndex = 0; sourceIndex < metrics.CharacterSet.Length; sourceIndex++)
        {
            var character = metrics.CharacterSet[sourceIndex];
            if (!metrics.Advances.TryGetValue(character, out var advance))
            {
                texture.Dispose();
                throw new InvalidDataException(
                    $"Bitmap font '{fontName}' has no spacing data for U+{(int)character:X4} ('{character}')."
                );
            }

            glyphEntries.Add(new GlyphEntry(character, sourceIndex, advance));
        }

        // Empire 7's Construct sheet omits space from the visible character row but
        // includes its advance in the spacing table. The first unused grid cell is
        // transparent, making it a safe source rectangle for the space glyph.
        if (!glyphEntries.Any(entry => entry.Character == ' '))
        {
            var advance = metrics.Advances.TryGetValue(' ', out var spaceAdvance)
                ? spaceAdvance
                : Math.Max(1, metrics.CellWidth / 4);

            glyphEntries.Add(new GlyphEntry(' ', metrics.CharacterSet.Length, advance));
        }

        glyphEntries.Sort((left, right) => left.Character.CompareTo(right.Character));

        var glyphBounds = new List<XNARectangle>(glyphEntries.Count);
        var cropping = new List<XNARectangle>(glyphEntries.Count);
        var characters = new List<char>(glyphEntries.Count);
        var kerning = new List<XNAVector3>(glyphEntries.Count);

        foreach (var entry in glyphEntries)
        {
            var column = entry.SourceIndex % columns;
            var row = entry.SourceIndex / columns;

            glyphBounds.Add(
                new XNARectangle(
                    column * metrics.CellWidth,
                    row * metrics.CellHeight,
                    metrics.CellWidth,
                    metrics.CellHeight
                )
            );

            cropping.Add(new XNARectangle(0, 0, metrics.CellWidth, metrics.CellHeight));
            characters.Add(entry.Character);
            kerning.Add(new XNAVector3(0f, entry.Advance, 0f));
        }

        var defaultCharacter = characters.Contains('?') ? '?' : characters[0];
        _spriteFont = new SpriteFont(
            texture,
            glyphBounds,
            cropping,
            characters,
            metrics.CellHeight,
            0f,
            kerning,
            defaultCharacter
        );
    }

    protected override FontSizeRenderer<SpriteFont> CreateRendererFor(int size)
    {
        // The source art is a 7-ish pixel-high glyph inside a 16px cell. Whole-number
        // scaling is deliberate: PointClamp then reproduces the exact source pixels
        // without introducing TTF-style smoothing or fractional-pixel distortion.
        var renderScale = Math.Max(1, (int)MathF.Floor((size + 4f) / 8f));
        return new SpriteFontRenderer(_spriteFont, renderScale);
    }

    private static BitmapFontMetrics ParseMetrics(string metricsPath)
    {
        var raw = File.ReadAllText(metricsPath);

        var widthMatch = Regex.Match(raw, @"Character width:\s*(\d+)", RegexOptions.IgnoreCase);
        var heightMatch = Regex.Match(raw, @"Character height:\s*(\d+)", RegexOptions.IgnoreCase);
        var characterSetMatch = Regex.Match(
            raw,
            @"Character set:[^\r\n]*\r?\n([^\r\n]+)",
            RegexOptions.IgnoreCase
        );

        if (!widthMatch.Success || !heightMatch.Success || !characterSetMatch.Success)
        {
            throw new InvalidDataException($"Unable to parse bitmap font metrics from '{metricsPath}'.");
        }

        var cellWidth = int.Parse(widthMatch.Groups[1].Value);
        var cellHeight = int.Parse(heightMatch.Groups[1].Value);
        var characterSet = characterSetMatch.Groups[1].Value;

        if (cellWidth < 1 || cellHeight < 1 || characterSet.Length == 0)
        {
            throw new InvalidDataException($"Bitmap font metrics in '{metricsPath}' are invalid.");
        }

        var advances = new Dictionary<char, int>();
        var spacingMatches = Regex.Matches(raw, "\\[(\\d+),\"((?:\\\\.|[^\"])*)\"\\]");
        foreach (Match spacingMatch in spacingMatches)
        {
            var advance = int.Parse(spacingMatch.Groups[1].Value);
            var encodedCharacters = spacingMatch.Groups[2].Value;
            var decodedCharacters = Regex.Unescape(encodedCharacters);

            foreach (var character in decodedCharacters)
            {
                advances[character] = advance;
            }
        }

        return new BitmapFontMetrics(cellWidth, cellHeight, characterSet, advances);
    }

    private sealed record GlyphEntry(char Character, int SourceIndex, int Advance);

    private sealed record BitmapFontMetrics(
        int CellWidth,
        int CellHeight,
        string CharacterSet,
        Dictionary<char, int> Advances
    );
}
