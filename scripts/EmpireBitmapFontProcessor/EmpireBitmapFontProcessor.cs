using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;

namespace CorpsRoyaux.Content.Pipeline;

[ContentProcessor(DisplayName = "Empire Bitmap Font Processor")]
public sealed class EmpireBitmapFontProcessor : ContentProcessor<Texture2DContent, SpriteFontContent>
{
    public string MetricsFile { get; set; } = "empire7.metrics.json";

    public int Scale { get; set; } = 1;

    public override SpriteFontContent Process(Texture2DContent input, ContentProcessorContext context)
    {
        if (Scale < 1)
        {
            throw new InvalidContentException("Empire bitmap font scale must be at least 1.", input.Identity);
        }

        var sourceFilename = input.Identity?.SourceFilename;
        var sourceDirectory = string.IsNullOrWhiteSpace(sourceFilename)
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(Path.GetFullPath(sourceFilename)) ?? Directory.GetCurrentDirectory();

        var metricsPath = Path.IsPathRooted(MetricsFile)
            ? MetricsFile
            : Path.Combine(sourceDirectory, MetricsFile);

        metricsPath = Path.GetFullPath(metricsPath);
        if (!File.Exists(metricsPath))
        {
            throw new InvalidContentException($"Empire bitmap font metrics were not found: {metricsPath}", input.Identity);
        }

        context.AddDependency(metricsPath);

        var metrics = JsonSerializer.Deserialize<EmpireBitmapMetrics>(
            File.ReadAllText(metricsPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        if (metrics is null || metrics.Characters.Count == 0)
        {
            throw new InvalidContentException("Empire bitmap font metrics are empty or invalid.", input.Identity);
        }

        if (metrics.CellWidth < 1 || metrics.CellHeight < 1 || metrics.Columns < 1)
        {
            throw new InvalidContentException("Empire bitmap font grid dimensions are invalid.", input.Identity);
        }

        var cellWidth = metrics.CellWidth * Scale;
        var cellHeight = metrics.CellHeight * Scale;
        var entries = new List<GlyphEntry>(metrics.Characters.Count + 1);

        for (var index = 0; index < metrics.Characters.Count; ++index)
        {
            var codePoint = metrics.Characters[index];
            if (codePoint is < char.MinValue or > char.MaxValue)
            {
                throw new InvalidContentException($"Unsupported Empire character code point: {codePoint}.", input.Identity);
            }

            var character = (char)codePoint;
            var sourceRect = CellRectangle(index, metrics.Columns, cellWidth, cellHeight);
            var advance = GetAdvance(metrics, codePoint) * Scale;
            entries.Add(new GlyphEntry(character, sourceRect, advance));
        }

        // The supplied PNG contains unused cells after the character set. The first
        // empty cell is used as a transparent glyph for a real space character.
        if (entries.All(entry => entry.Character != ' '))
        {
            var sourceRect = CellRectangle(metrics.Characters.Count, metrics.Columns, cellWidth, cellHeight);
            entries.Add(new GlyphEntry(' ', sourceRect, GetAdvance(metrics, ' ') * Scale));
        }

        entries.Sort((left, right) => left.Character.CompareTo(right.Character));

        var glyphs = new List<Rectangle>(entries.Count);
        var cropping = new List<Rectangle>(entries.Count);
        var characterMap = new List<char>(entries.Count);
        var kerning = new List<Vector3>(entries.Count);

        foreach (var entry in entries)
        {
            glyphs.Add(entry.SourceRectangle);
            cropping.Add(new Rectangle(0, 0, cellWidth, cellHeight));
            characterMap.Add(entry.Character);
            kerning.Add(new Vector3(0f, entry.Advance, 0f));
        }

        return new SpriteFontContent
        {
            Texture = input,
            Glyphs = glyphs,
            Cropping = cropping,
            CharacterMap = characterMap,
            Kerning = kerning,
            HorizontalSpacing = 0f,
            VerticalLineSpacing = cellHeight,
            DefaultCharacter = '?',
            FontName = "Empire 7 Bitmap",
            FontSize = cellHeight,
            Style = FontDescriptionStyle.Regular,
        };
    }

    private static Rectangle CellRectangle(int index, int columns, int cellWidth, int cellHeight)
    {
        var column = index % columns;
        var row = index / columns;
        return new Rectangle(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
    }

    private static int GetAdvance(EmpireBitmapMetrics metrics, int codePoint)
    {
        return metrics.Widths.TryGetValue(codePoint.ToString(), out var width) && width > 0
            ? width
            : Math.Max(1, metrics.CellWidth / 2);
    }

    private sealed record GlyphEntry(char Character, Rectangle SourceRectangle, int Advance);

    private sealed class EmpireBitmapMetrics
    {
        public int CellWidth { get; set; } = 16;

        public int CellHeight { get; set; } = 16;

        public int Columns { get; set; } = 26;

        public List<int> Characters { get; set; } = [];

        public Dictionary<string, int> Widths { get; set; } = [];
    }
}
