using System.Text.RegularExpressions;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Utilities;
using Intersect.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Intersect.Client.Framework.Gwen.Control;


/// <summary>
///     Multiline label with text chunks having different color/font.
/// </summary>
public partial class RichLabel : Base
{
    private readonly string[] _newlines = new[]{ Environment.NewLine, "\n" }.Distinct().ToArray();
    private readonly List<TextBlock> _textBlocks = [];
    private readonly List<Label> _formattedLabels = [];

    private IFont? _font;
    private int _fontSize = 10;

    private string? _fontInfo;

    private bool _needsRebuild;

    public List<Label> FormattedLabels => _formattedLabels;

    public IFont? Font
    {
        get => _font;
        set
        {
            _font = value;
            _fontInfo = value is { } font ? $"{font.Name},{_fontSize}" : null;
        }
    }

    public string? FontName
    {
        get => _font?.Name;
        set => Font = GameContentManager.Current.GetFont(value);
    }

    public int FontSize
    {
        get => _fontSize;
        set
        {
            _fontSize = value;
            _fontInfo = _font is { } font ? $"{font.Name},{value}" : null;
        }
    }

    public string? Text
    {
        get => string.Join('\n', _textBlocks.Select(block => block.Text));
        set
        {
            ClearText();
            AddText(value ?? string.Empty, default(Color));
        }
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RichLabel" /> class.
    /// </summary>
    /// <param name="parent">Parent control.</param>
    /// <param name="name"></param>
    public RichLabel(Base parent, string? name = default) : base(parent, name)
    {
    }

    /// <summary>
    ///     Adds a line break to the control.
    /// </summary>
    public void AddLineBreak()
    {
        var block = new TextBlock { Type = BlockType.NewLine };
        _textBlocks.Add(block);
    }

    /// <inheritdoc />
    public override void LoadJson(JToken token, bool isRoot = default)
    {
        base.LoadJson(token, isRoot);

        if (token is not JObject obj)
        {
            return;
        }

        if (obj["Font"] != null && obj["Font"].Type != JTokenType.Null)
        {
            var fontArr = ((string)obj["Font"]).Split(',');
            _fontInfo = (string)obj["Font"];
            _fontSize = int.Parse(fontArr[1]);
            _font = GameContentManager.Current.GetFont(fontArr[0]);
        }

        if (obj.TryGetValue(nameof(FontName), out var tokenFontName) &&
            tokenFontName is JValue { Type: JTokenType.String } valueFontName)
        {
            FontName = valueFontName.Value<string>();
        }

        if (obj.TryGetValue(nameof(FontSize), out var tokenFontSize) &&
            tokenFontSize is JValue { Type: JTokenType.Integer } valueFontSize)
        {
            FontSize = valueFontSize.Value<int>();
        }
    }

    /// <param name="isRoot"></param>
    /// <param name="onlySerializeIfNotEmpty"></param>
    /// <inheritdoc />
    public override JObject? GetJson(bool isRoot = false, bool onlySerializeIfNotEmpty = false)
    {
        var serializedProperties = base.GetJson(isRoot, onlySerializeIfNotEmpty);
        if (serializedProperties is null)
        {
            return null;
        }

        serializedProperties.Add("Font", _fontInfo);

        return base.FixJson(serializedProperties);
    }

    public GwenEventHandler<EventArgs>? Rebuilt;

    /// <summary>
    ///     Adds text to the control via the properties of a <see cref="Label"/>
    /// </summary>
    /// <param name="text">Text to add</param>
    /// <param name="template">Label to use as a template</param>
    public void AddText(string? text, Label? template)
    {
        AddText(
            text,
            template?.TextColor,
            template?.CurAlignments.FirstOrDefault(Alignments.Left) ?? Alignments.Left,
            template?.Font,
            template?.FontSize ?? default
        );
    }

    public void AddText(string? text, Color? color) => AddText(text, color, Alignments.Left);

    /// <summary>
    ///     Adds text to the control.
    /// </summary>
    /// <param name="text">Text to add.</param>
    /// <param name="color">Text color.</param>
    /// <param name="alignment"></param>
    /// <param name="font">Font to use.</param>
    /// <param name="fontSize"></param>
    public void AddText(string? text, Color? color, Alignments alignment, IFont? font = default, int fontSize = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (text.Contains("\\c"))
        {
            var colorizedSegments = TextColorParser.Parse(text, color);
            foreach (var segment in colorizedSegments)
            {
                AddText(segment.Text, segment.Color ?? color, alignment, font);
            }

            return;
        }

        font ??= _font;

        if (fontSize == default)
        {
            fontSize = _fontSize;
        }

        var lines = text.Split(_newlines, StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                AddLineBreak();
            }

            var block = new TextBlock
            {
                Type = BlockType.Text,
                Text = lines[i],
                Color = color,
                Font = font,
                FontSize = fontSize,
                Alignment = alignment,
            };

            _textBlocks.Add(block);
            InvalidateRebuild();
        }
    }

    public void AppendText(string text, Label? template)
    {
        AppendText(
            text,
            template?.TextColor,
            template?.CurAlignments.FirstOrDefault(Alignments.Left) ?? Alignments.Left,
            template?.Font
        );
    }

    public void AppendText(string text) => AppendText(text: text, color: null, alignment: Alignments.Left, font: null);

    public void AppendText(string text, Color? color, Alignments alignment, IFont? font = null, int fontSize = default)
    {
        if (_textBlocks.Count < 1)
        {
            AddText(text, color, alignment, font);
            return;
        }

        if (text.StartsWith('\n'))
        {
            AddText(text, color, alignment, font);
            return;
        }

        if (text.StartsWith("\r\n"))
        {
            AddText(text, color, alignment, font);
            return;
        }

        var lastTextBlock = _textBlocks.Last();
        var lines = text.Split(_newlines, StringSplitOptions.TrimEntries);
        var firstLine = lines.FirstOrDefault();

        var remainingLines = lines.AsEnumerable();

        if (firstLine is not null)
        {
            var appendColor = color ?? lastTextBlock.Color;
            var appendAlignment = alignment;
            var appendFont = font ?? lastTextBlock.Font;
            var appendFontSize = fontSize == default ? lastTextBlock.FontSize : fontSize;

            if (appendAlignment != lastTextBlock.Alignment ||
                appendColor != lastTextBlock.Color ||
                appendFont != lastTextBlock.Font)
            {
                AddText(text, color, alignment, font, appendFontSize);
                return;
            }

            remainingLines = remainingLines.Skip(1);
            lastTextBlock.Text += text;
            var label = lastTextBlock.Labels.LastOrDefault();
            if (label == null)
            {
                InvalidateRebuild();
            }
            else
            {
                label.Text += text;
                label.SizeToContents();
                if (label.OuterBounds.Right > InnerBounds.Right)
                {
                    InvalidateRebuild();
                }
            }
        }

        foreach (var line in remainingLines)
        {
            AddText(line, color, alignment, font);
        }
    }

    public void InvalidateRebuild()
    {
        if (!_needsRebuild)
        {
            ApplicationContext.CurrentContext.Logger.LogTrace(
                "Requesting rebuild of {NodeType} ({NodeName})",
                nameof(RichLabel),
                ParentQualifiedName
            );

            _needsRebuild = true;
        }

        Invalidate();
    }

    public void ForceImmediateRebuild() => Rebuild();

    protected override Point ApplyDockFillOnSizeToChildren(Point size, Point internalSize)
    {
        if (size.X >= internalSize.X && size.Y >= internalSize.Y)
        {
            return size;
        }

        return base.ApplyDockFillOnSizeToChildren(size, internalSize);
    }

    public override Point GetChildrenSize()
    {
        var childrenSize = base.GetChildrenSize();
        return childrenSize;
    }

    protected void SplitLabel(
        string text,
        IFont font,
        int fontSize,
        TextBlock block,
        ref int x,
        ref int y,
        ref int lineHeight,
        int initialX,
        int availableWidth
    )
    {
        if (string.IsNullOrEmpty(text)) return;

        // Iterative wrapping must consume at least one text element every pass. The old
        // recursive word splitter could recurse forever on leading spaces or a narrow viewport.
        var elements = System.Globalization.StringInfo.ParseCombiningCharacters(text);
        int Offset(int element) => element == elements.Length ? text.Length : elements[element];
        var first = 0;
        while (first < elements.Length)
        {
            var start = Offset(first);
            var firstGlyph = text[start..Offset(first + 1)];
            if (x > initialX && Skin.Renderer.MeasureText(font, fontSize, firstGlyph).X > availableWidth - x)
                CreateNewline(ref x, ref y, Math.Max(1, lineHeight), initialX);

            var low = first + 1;
            var high = elements.Length;
            var end = first;
            while (low <= high)
            {
                var middle = low + (high - low) / 2;
                var width = Skin.Renderer.MeasureText(font, fontSize, text[start..Offset(middle)]).X;
                if (width <= availableWidth - x) { end = middle; low = middle + 1; }
                else high = middle - 1;
            }
            // A glyph wider than the viewport still gets emitted once; never retry it indefinitely.
            end = Math.Max(first + 1, end);
            if (end < elements.Length)
            {
                for (var boundary = end; boundary > first; boundary--)
                    if (char.IsWhiteSpace(text[Offset(boundary) - 1])) { end = boundary; break; }
            }
            CreateLabel(text[start..Offset(end)], block, ref x, ref y, ref lineHeight, initialX, availableWidth, true);
            first = end;
            if (first < elements.Length && x > initialX)
                CreateNewline(ref x, ref y, Math.Max(1, lineHeight), initialX);
        }
    }

    protected void CreateLabel(
        string text,
        TextBlock block,
        ref int x,
        ref int y,
        ref int lineHeight,
        int initialX,
        int availableWidth,
        bool noSplit
    )
    {
        // Use default font or is one set?
        var font = Skin.DefaultFont;
        if (block.Font != null)
        {
            font = block.Font;
        }

        var fontSize = block.FontSize;

        // This string is too long for us, split it up.
        var textSize = Skin.Renderer.MeasureText(font, fontSize, text);

        if (lineHeight == -1)
        {
            lineHeight = textSize.Y;
        }

        if (!noSplit)
        {
            if (x + textSize.X > availableWidth)
            {
                SplitLabel(
                    text,
                    font,
                    fontSize,
                    block,
                    ref x,
                    ref y,
                    ref lineHeight,
                    initialX,
                    availableWidth
                );

                return;
            }
        }

        // Wrap only after existing content; oversized single glyphs must not add an empty line.
        if (x > initialX && x + textSize.X > availableWidth)
        {
            CreateNewline(ref x, ref y, lineHeight, initialX);
        }

        var label = new Label(this)
        {
            X = x,
            Y = y,
            Alignment = [block.Alignment],
            AutoSizeToContents = true,
            Font = font,
            FontSize = block.FontSize,
            RestrictToParent = false,
            Text = x == initialX ? text.TrimStart(' ') : text,
            TextAlign = Pos.None,
            TextColorOverride = block.Color,
        };

        label.SizeToContents();

        block.Labels.Add(label);
        _formattedLabels.Add(label);

        x += label.Width;

        if (x > availableWidth)
        {
            CreateNewline(ref x, ref y, lineHeight, initialX);
        }
    }

    protected void CreateNewline(ref int x, ref int y, int lineHeight, int initialX)
    {
        x = initialX;
        y += Math.Max(1, lineHeight);
    }

    public void ClearText()
    {
        _textBlocks.Clear();
        InvalidateRebuild();
    }

    private void Rebuild()
    {
        _needsRebuild = false;

        DeleteAllChildren();
        _formattedLabels.Clear();

        var bounds = Bounds;
        var padding = Padding;
        var initialX = padding.Left;
        // availableWidth is the right content edge, in the same coordinate system as x.
        var availableWidth = bounds.Width - padding.Right;
        if (availableWidth <= initialX) { Rebuilt?.Invoke(this, EventArgs.Empty); return; }
        var x = initialX;
        var y = padding.Top;
        var lineHeight = -1;

        foreach (var block in _textBlocks)
        {
            block.Labels.Clear();

            if (block.Type == BlockType.NewLine)
            {
                CreateNewline(ref x, ref y, lineHeight, initialX);

                continue;
            }

            if (block.Type == BlockType.Text)
            {
                CreateLabel(block.Text, block, ref x, ref y, ref lineHeight, initialX, availableWidth, false);

                continue;
            }
        }

        var resizeX = !Dock.HasFlag(Pos.Fill) || _dockFillSize == default || _dockFillSize.X != Width;
        base.SizeToChildren(resizeX: resizeX, resizeY: true);
        Rebuilt?.Invoke(this, EventArgs.Empty);
    }

    // protected override void OnBoundsChanged(Rectangle oldBounds, Rectangle newBounds)
    // {
    //     base.OnBoundsChanged(oldBounds, newBounds);
    //
    //     InvalidateRebuild();
    // }

    protected override void OnSizeChanged(Point oldSize, Point newSize)
    {
        base.OnSizeChanged(oldSize, newSize);

        if (oldSize.X == newSize.X)
        {
            if (newSize.X == _dockFillSize.X && newSize.Y <= _dockFillSize.Y)
            {
                return;
            }
        }

        ApplicationContext.CurrentContext.Logger.LogDebug(
            "Rebuilding due to size change from {OldSize} to {NewSize} (_dockFillSize={DockFillSize})",
            oldSize,
            newSize,
            _dockFillSize
        );
        InvalidateRebuild();
    }

    /// <summary>
    ///     Lays out the control's interior according to alignment, padding, dock etc.
    /// </summary>
    /// <param name="skin">Skin to use.</param>
    protected override void Layout(Skin.Base skin)
    {
        base.Layout(skin);

        if (_needsRebuild)
        {
            Rebuild();
        }

        // align bottoms. this is still not ideal, need to take font metrics into account.
        Base prev = null;
        foreach (var child in Children)
        {
            if (prev != null && child.Y == prev.Y)
            {
                Align.PlaceRightBottom(child, prev);
            }

            prev = child;
        }
    }

    protected class TextBlock
    {
        public BlockType Type;

        public string Text;

        public Color? Color;

        public IFont? Font;

        public int FontSize;

        public Alignments Alignment;

        public readonly List<Label> Labels = [];
    }

    protected enum BlockType
    {

        Text,

        NewLine

    }

}
