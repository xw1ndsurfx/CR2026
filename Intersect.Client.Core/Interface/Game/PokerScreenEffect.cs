using Intersect.Client.Core;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core.GameObjects.Animations;

namespace Intersect.Client.Interface.Game;

/// <summary>Local, non-interactive screen overlay. Never creates a map/proximity animation.</summary>
internal sealed class PokerScreenEffect : IDisposable
{
    private sealed class Layer
    {
        public required ImagePanel Image;
        public int Columns, Rows, Frames, Speed;
    }
    private readonly Canvas _canvas;
    private readonly Layer[] _layers;
    private long _started;
    private bool _disposed;

    public PokerScreenEffect(Canvas canvas, string name)
    {
        _canvas = canvas;
        name = string.IsNullOrWhiteSpace(name) ? "Effect" : name;
        _layers = [Make("Poker" + name + "Lower"), Make("Poker" + name + "Upper")];
        Layer Make(string name) => new()
        {
            Image = new ImagePanel(canvas, name)
            {
                IsHidden = true, MouseInputEnabled = false, KeyboardInputEnabled = false,
                ShouldDrawBackground = false,
            },
        };
    }

    public void Play(Guid id, string? fallbackSound = null)
    {
        if (_disposed) return;
        foreach (var layer in _layers) { layer.Frames = 0; layer.Image.IsHidden = true; }
        var animation = id == Guid.Empty ? null : AnimationDescriptor.Get(id);
        var sound = !string.IsNullOrWhiteSpace(animation?.Sound) ? animation.Sound : fallbackSound;
        if (!string.IsNullOrWhiteSpace(sound)) Audio.AddGameSound(sound, false);
        if (animation == null) return;
        Configure(_layers[0], animation.Lower);
        Configure(_layers[1], animation.Upper);
        _started = Environment.TickCount64;
    }

    private static void Configure(Layer target, AnimationLayer? source)
    {
        if (source == null || string.IsNullOrWhiteSpace(source.Sprite) ||
            source.XFrames is < 1 or > 128 || source.YFrames is < 1 or > 128 || source.FrameCount < 1) return;
        var texture = GameContentManager.Current?.GetTexture(TextureType.Animation, source.Sprite);
        if (texture == null || texture.Width < source.XFrames || texture.Height < source.YFrames) return;
        target.Image.Texture = texture;
        target.Columns = source.XFrames;
        target.Rows = source.YFrames;
        target.Frames = Math.Min(source.FrameCount, source.XFrames * source.YFrames);
        target.Speed = Math.Clamp(source.FrameSpeed, 10, 1000);
    }

    public void Update()
    {
        if (_disposed) return;
        var elapsed = Math.Max(0, Environment.TickCount64 - _started);
        foreach (var layer in _layers)
        {
            var frame = layer.Speed > 0 ? elapsed / layer.Speed : long.MaxValue;
            if (layer.Frames == 0 || frame >= layer.Frames || elapsed >= 8000 || layer.Image.Texture is not { } texture)
            { layer.Image.IsHidden = true; continue; }
            var w = texture.Width / layer.Columns;
            var h = texture.Height / layer.Rows;
            var scale = Math.Min(Math.Min(320f, _canvas.Width) / w, Math.Min(240f, _canvas.Height) / h);
            var width = Math.Max(1, (int)(w * scale));
            var height = Math.Max(1, (int)(h * scale));
            layer.Image.SetTextureRect((int)(frame % layer.Columns) * w, (int)(frame / layer.Columns) * h, w, h);
            layer.Image.SetBounds((_canvas.Width - width) / 2, (_canvas.Height - height) / 2, width, height);
            layer.Image.IsHidden = false;
            layer.Image.BringToFront();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var layer in _layers)
        {
            // Gwen Base.Dispose does not detach itself. Remove the owned root overlay first
            // so later canvas teardown cannot dispose the same control for a second time.
            layer.Image.Parent?.RemoveChild(layer.Image, false);
            layer.Image.Dispose();
        }
    }
}
