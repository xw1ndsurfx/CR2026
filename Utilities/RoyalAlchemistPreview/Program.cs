using System.Collections;
using System.Reflection;
using System.Numerics;
using Intersect;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.MiniGames.Potions;
using Intersect.Network.Packets.Server;
using Bitmap = System.Drawing.Bitmap;
using Color = Intersect.Color;
using DrawingColor = System.Drawing.Color;
using DrawingRect = System.Drawing.Rectangle;
using GameRect = Intersect.Client.Framework.GenericClasses.Rectangle;
using GamePoint = Intersect.Point;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var root = Path.GetFullPath(args[0]);
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        var content = new TestContentManager(); Globals.ContentManager = content;
        using var background = new BitmapTexture(Path.Combine(root, "resources/gui/royal_alchemist_background_v1.png"));
        using var atlas = new BitmapTexture(Path.Combine(root, "resources/gui/royal_alchemist_pieces_v4.png"));
        content.AddGuiTexture("royal_alchemist_background_v1", background);
        content.AddGuiTexture("royal_alchemist_pieces_v4", atlas);
        content.AddGuiTexture("royal_alchemist_background_v1.png", background);
        content.AddGuiTexture("royal_alchemist_pieces_v4.png", atlas);
        foreach (var size in new[] { (Width: 1280, Height: 720), (Width: 1920, Height: 1080) })
        {
            using var image = new Bitmap(size.Width, size.Height);
            using var renderer = new PreviewRenderer(image);
            using var skin = new PreviewSkin(renderer);
            using var canvas = new Canvas(skin) { Size = new GamePoint(size.Width, size.Height) };
            var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.PotionWindow", true)!;
            var modelType = typeof(GameInterface).Assembly.GetType("Intersect.Client.MiniGames.PotionClientModel", true)!;
            var scene = (Intersect.Client.Framework.Gwen.Control.Base)Activator.CreateInstance(type,
                [canvas, new Action<PotionRequestKind, int, Guid, int>((_, _, _, _) => { })])!;
            try
            {
                var recipe = Guid.NewGuid(); var player = Guid.NewGuid();
                var state = new PotionSessionState { RecipeId = recipe, RecipeName = "Royal restorative", RecipeRound = 1,
                    RequiredLevel = 1, Board = new int[80], CurrentFirst = 1, CurrentSecond = 5, NextFirst = 9, NextSecond = 13,
                    OutputItemId = Guid.NewGuid(), OutputItemName = "Royal restorative", OutputQuantity = 1, CompletionExperience = 25, Level = 2,
                    Experience = 160, Score = 240, Requirements = [new() { Family = 0, Level = 2, Needed = 3 }, new() { Family = 1, Level = 2, Needed = 2 }, new() { Family = 2, Level = 3, Needed = 1 }],
                    RecipeChoices = [new() { Id = recipe, Name = "Royal restorative", RequiredLevel = 1, OutputItemName = "Royal restorative", OutputQuantity = 1, CompletionExperience = 25, Unlocked = true }],
                    Assets = new() { RotateSound = "", DropSound = "", MergeSound = "", CompleteSound = "", GameOverSound = "" } };
                for (var row = 0; row < 4; row++) for (var tier = 0; tier < 4; tier++) state.Board[(6 + row) * 8 + tier * 2] = 1 + row * 4 + tier;
                var packet = new PotionStatePacket { PlayerId = player, SessionId = Guid.NewGuid(), Sequence = 1, State = state };
                var model = Activator.CreateInstance(modelType)!;
                if (!(bool)modelType.GetMethod("Apply")!.Invoke(model, [packet, player, Environment.TickCount64])!) throw new Exception("Preview fixture rejected.");
                type.GetMethod("Update")!.Invoke(scene, [model]);
                type.GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(scene, [skin]);
                if (!renderer.HasWholeViewportBackground) throw new Exception("Alchemy left an uncovered viewport region.");
                foreach (var placement in (IEnumerable)type.GetField("_placements", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(scene)!)
                {
                    var control = (Intersect.Client.Framework.Gwen.Control.Base)placement.GetType().GetProperty("Control")!.GetValue(placement)!;
                    if (control.IsHidden) continue;
                    var text = control.GetType().GetProperty("Text")?.GetValue(control)?.ToString();
                    if (string.IsNullOrEmpty(text)) continue;
                    var bounds = new DrawingRect(control.X, control.Y, control.Width, control.Height);
                    var fontSize = (int?)control.GetType().GetProperty("FontSize")?.GetValue(control) ?? 14;
                    var button = control is Intersect.Client.Framework.Gwen.Control.Button;
                    var color = !button && control.GetType().GetProperty("TextColorOverride")?.GetValue(control) is Color c && c.A > 0 ? c : Color.White;
                    var centered = button || control.GetType().GetProperty("TextAlign")?.GetValue(control) is Pos alignment && alignment == Pos.Center;
                    renderer.Caption(bounds, text, fontSize, color, button, centered, control.IsDisabled);
                }
                image.Save(Path.Combine(output, $"alchemy-{size.Width}x{size.Height}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            finally { type.GetMethod("Destroy")!.Invoke(scene, null); }
        }
        if (atlas.AlphaReads != 2) throw new Exception("The atlas must be read only once per scene, not once per ingredient.");
        System.Console.WriteLine("Rendered the actual alchemy scene and control bounds at 1280x720 and 1920x1080.");
    }
}

internal sealed class PreviewSkin : SkinBase
{
    public PreviewSkin(RendererBase renderer) : base(renderer)
    {
        DefaultFont = new TestFont(); DefaultFontSize = 12;
        Colors.Label.Normal = Colors.Label.Disabled = Color.White;
    }
}

internal sealed class TestFont : IFont
{
    public string Name => "sourcesanspro";
    public ICollection<int> SupportedSizes { get; } = [10, 12, 14, 18, 22];
    public int PickBestMatchFor(int size) => size;
    public int GetNextFontSize(int startSize, int direction, int limit = 0) => startSize + direction;
}

internal sealed class PreviewRenderer : RendererBase
{
    private readonly System.Drawing.Graphics _graphics;
    private readonly System.Drawing.Size _size;
    public bool HasWholeViewportBackground { get; private set; }
    public PreviewRenderer(Bitmap target)
    {
        _graphics = System.Drawing.Graphics.FromImage(target);
        _size = target.Size;
        _graphics.Clear(DrawingColor.Black);
        _graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        _graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
    }
    private static DrawingColor Draw(Color c) => DrawingColor.FromArgb(c.A, c.R, c.G, c.B);
    public override void DrawFilledRect(GameRect rect)
    {
        using var brush = new System.Drawing.SolidBrush(Draw(DrawColor));
        _graphics.FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);
    }
    public override void DrawLine(int x, int y, int a, int b)
    {
        using var pen = new System.Drawing.Pen(Draw(DrawColor)); _graphics.DrawLine(pen, x, y, a, b);
    }
    public override void DrawTexturedRect(IGameTexture? texture, GameRect targetBounds, Color color, float u1 = 0, float v1 = 0, float u2 = 1, float v2 = 1)
    {
        if (texture is not BitmapTexture source) return;
        if (source.Name.Contains("background") && targetBounds.X == 0 && targetBounds.Y == 0 && targetBounds.Width == _size.Width && targetBounds.Height == _size.Height)
            HasWholeViewportBackground = true;
        using var attributes = new System.Drawing.Imaging.ImageAttributes();
        attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix00 = color.R / 255f, Matrix11 = color.G / 255f, Matrix22 = color.B / 255f, Matrix33 = color.A / 255f, Matrix44 = 1 });
        _graphics.DrawImage(source.Bitmap, new DrawingRect(targetBounds.X, targetBounds.Y, targetBounds.Width, targetBounds.Height),
            source.Width * u1, source.Height * v1, source.Width * (u2 - u1), source.Height * (v2 - v1), System.Drawing.GraphicsUnit.Pixel, attributes);
    }
    public void Caption(DrawingRect rect, string text, int size, Color color, bool button, bool centered, bool disabled)
    {
        if (button)
        {
            var radius = Math.Max(3f, rect.Height / 8f);
            var diameter = radius * 2;
            using var shape = new System.Drawing.Drawing2D.GraphicsPath();
            shape.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            shape.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            shape.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            shape.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            shape.CloseFigure();
            using var fill = new System.Drawing.Drawing2D.LinearGradientBrush(rect,
                disabled ? DrawingColor.FromArgb(79, 48, 46) : DrawingColor.FromArgb(185, 155, 133),
                disabled ? DrawingColor.FromArgb(61, 34, 34) : DrawingColor.FromArgb(150, 117, 95), 90f);
            _graphics.FillPath(fill, shape);
            using var border = new System.Drawing.Pen(DrawingColor.FromArgb(37, 21, 14), 2);
            _graphics.DrawPath(border, shape);
        }
        using var font = new System.Drawing.Font("Segoe UI", Math.Max(9, size), System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
        using var brush = new System.Drawing.SolidBrush(Draw(color));
        using var format = new System.Drawing.StringFormat { Alignment = centered ? System.Drawing.StringAlignment.Center : System.Drawing.StringAlignment.Near, LineAlignment = System.Drawing.StringAlignment.Center };
        _graphics.DrawString(text, font, brush, rect, format);
    }
    public override GamePoint MeasureText(IFont? font, int fontSize, string? text, float scale = 1f) => new((int)((text?.Length ?? 0) * fontSize * .6f), fontSize + 3);
    public override void Dispose() { _graphics.Dispose(); base.Dispose(); }
}

internal sealed class BitmapTexture : IGameTexture
{
    public Bitmap Bitmap { get; }
    public int AlphaReads { get; private set; }
    public BitmapTexture(string path) { Bitmap = new Bitmap(path); Name = Path.GetFileName(path); }
    public string Id => Name;
    public string Name { get; }
    public override string ToString() => Name;
    public bool IsDisposed => false;
    public bool IsLoaded => true;
    public bool IsMissingOrCorrupt => false;
    public bool IsPinned => true;
    public long AccessTime => 0;
    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;
    public int Area => Width * Height;
    public Vector2 Dimensions => new(Width, Height);
    public Vector2 Center => Dimensions / 2;
    public Intersect.Client.Framework.GenericClasses.FloatRect Bounds => new(0, 0, Width, Height);
    public AtlasReference? AtlasReference => null;
    public Color this[int x, int y] => GetPixel(x, y);
    public Color this[GamePoint p] => GetPixel(p.X, p.Y);
    public Color GetPixel(int x, int y) { var c = Bitmap.GetPixel(x, y); return new Color(c.A, c.R, c.G, c.B); }
    public byte[] ReadAlpha() { AlphaReads++; var data = new byte[Area]; for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++) data[y * Width + x] = Bitmap.GetPixel(x, y).A; return data; }
    public bool Unload() => false;
    public void Reload() { }
    public object GetTexture() => Bitmap;
    public T? GetTexture<T>() where T : class => Bitmap as T;
    public int CompareTo(IGameTexture? other) => string.CompareOrdinal(Name, other?.Name);
    public void Dispose() => Bitmap.Dispose();
    public event Action<IAsset>? Disposed { add { } remove { } }
    public event Action<IAsset>? Loaded { add { } remove { } }
    public event Action<IAsset>? Unloaded { add { } remove { } }
}
