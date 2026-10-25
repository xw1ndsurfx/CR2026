using Intersect.Editor.General;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Editor.Content;

namespace Intersect.Editor.Core;

public static partial class Graphics
{
    private static CinematicScene? _cinematicPreview;
    private static int _cinematicPreviewTime;

    public static Bitmap RenderCinematicMap(CinematicScene scene, int timeMs)
    {
        lock (GraphicsLock)
        {
            if (Globals.CurrentMap?.Id != scene.MapId) throw new InvalidOperationException("scene-map");
            EndSpriteBatch();
            var target = sCurrentTarget;
            var previousView = CurrentView;
            var hideEvents = Database.GridHideEvents;
            Database.GridHideEvents = false;
            // Native tile culling and overlays use CurrentView. Render the whole
            // map independently of the docked editor's scroll, zoom and size.
            CurrentView = new System.Drawing.Rectangle(0, 0,
                Options.Instance.Map.MapWidth * Options.Instance.Map.TileWidth,
                Options.Instance.Map.MapHeight * Options.Instance.Map.TileHeight);
            _cinematicPreview = scene; _cinematicPreviewTime = timeMs;
            try { return ScreenShotMap(); }
            finally { EndSpriteBatch(); _cinematicPreview = null; Database.GridHideEvents = hideEvents; CurrentView = previousView; SetRenderTarget(target); }
        }
    }

    private static void CinematicEventPosition(Guid mapId, Guid eventId, ref int x, ref int y)
    {
        if (_cinematicPreview is not { } scene || scene.MapId != mapId) return;
        var actor = scene.Actors.FirstOrDefault(a => a.TargetId == eventId);
        if (actor == null) return;
        var position = CinematicPreview.ActorPosition(scene, actor, _cinematicPreviewTime);
        x = (int)Math.Round(position.X * Options.Instance.Map.TileWidth);
        y = (int)Math.Round(position.Y * Options.Instance.Map.TileHeight);
    }

    private static void DrawCinematicAnimations(Microsoft.Xna.Framework.Graphics.RenderTarget2D target)
    {
        if (_cinematicPreview is not { } scene) return;
        foreach (var clip in scene.Clips.Where(c => c.Track == CinematicTrack.Animation && c.StartMs <= _cinematicPreviewTime))
        {
            if (AnimationDescriptor.Get(clip.ResourceId) is not { } animation) continue;
            foreach (var layer in new[] { animation.Lower, animation.Upper })
            {
                if (layer.FrameSpeed <= 0 || layer.FrameCount <= 0 || layer.XFrames <= 0 || layer.YFrames <= 0) continue;
                var frame = (_cinematicPreviewTime - clip.StartMs) / layer.FrameSpeed;
                if (frame >= (long)layer.FrameCount * (layer.LoopCount + 1)) continue;
                frame %= layer.FrameCount;
                var texture = GameContentManager.GetTexture(GameContentManager.TextureType.Animation, layer.Sprite);
                if (texture == null) continue;
                var width = texture.Width / layer.XFrames; var height = texture.Height / layer.YFrames;
                var scale = Math.Clamp(layer.ScalePercent, 1, 1000) / 100f;
                DrawTexture(texture, new RectangleF(frame % layer.XFrames * width, frame / layer.XFrames * height, width, height),
                    new RectangleF((clip.X + .5f) * Options.Instance.Map.TileWidth + layer.OffsetX - width * scale / 2,
                        (clip.Y + .5f) * Options.Instance.Map.TileHeight + layer.OffsetY - height * scale / 2, width * scale, height * scale),
                    System.Drawing.Color.FromArgb(layer.Opacity, 255, 255, 255), target, Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied);
            }
        }
    }
}
