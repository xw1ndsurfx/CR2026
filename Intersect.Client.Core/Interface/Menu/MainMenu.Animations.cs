using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Configuration;
using Intersect.Framework.Core.GameObjects.Animations;
namespace Intersect.Client.Interface.Menu;

public partial class MainMenu
{
    private readonly List<(MenuAnimationDefinition Rule, AnimationLayer Layer, ImagePanel Panel)> _menuAnimations = [];
    private bool _animationsLoaded;
    private readonly long _animationStart = Environment.TickCount64;
    private void UpdateMenuAnimations()
    {
        UpdateMenuWeather();
        if (!_animationsLoaded)
        {
            _animationsLoaded = true;
            foreach (var rule in (ClientConfiguration.Instance.MenuAnimations ?? []).Take(32))
                foreach (var layer in new[] { rule.Lower, rule.Upper })
                {
                    if (layer == null || layer.XFrames is < 1 or > 128 || layer.YFrames is < 1 or > 128 || layer.FrameCount < 1)
                        continue;
                    var texture = GameContentManager.Current.GetTexture(TextureType.Animation, layer.Sprite);
                    if (texture == null)
                        continue;
                    var panel = new ImagePanel(_menuCanvas, "MainMenuAnimation" + _menuAnimations.Count) { Texture = texture, MouseInputEnabled = false, KeyboardInputEnabled = false, ShouldDrawBackground = false };
                    _menuAnimations.Add((rule, layer, panel));
                }
        }
        foreach (var (rule, layer, panel) in _menuAnimations)
        {
            var frame = (Environment.TickCount64 - _animationStart) / Math.Clamp(layer.FrameSpeed, 10, 60000);
            var count = Math.Min(layer.FrameCount, layer.XFrames * layer.YFrames);
            panel.IsHidden = !rule.Loop && frame >= count;
            if (panel.IsHidden || panel.Texture is not { } texture)
                continue;
            frame %= count;
            var w = texture.Width / layer.XFrames;
            var h = texture.Height / layer.YFrames;
            var scale = Math.Clamp(rule.ScalePercent, 1, 2000) / 100f * Math.Clamp(layer.ScalePercent, 1, 2000) / 100f;
            panel.SetTextureRect((int)(frame % layer.XFrames) * w, (int)(frame / layer.XFrames) * h, w, h);
            panel.SetBounds((int)(_menuCanvas.Width * Math.Clamp(rule.XPercent, 0, 100) / 100f - w * scale / 2 + layer.OffsetX * scale), (int)(_menuCanvas.Height * Math.Clamp(rule.YPercent, 0, 100) / 100f - h * scale / 2 + layer.OffsetY * scale), Math.Max(1, (int)(w * scale)), Math.Max(1, (int)(h * scale)));
            var brightness=(ClientConfiguration.Instance.MenuWeather??new()).BrightnessChannel();
            panel.RenderColor = new Color((byte)(layer.Opacity * (100 - Math.Clamp(rule.TransparencyPercent, 0, 100)) / 100), brightness, brightness, brightness);

        }
        foreach (var entry in _menuAnimations.Where(e => !e.Rule.Foreground).Reverse())
            entry.Panel.SendToBack();
        foreach (var entry in _menuAnimations.Where(e => e.Rule.Foreground))
            entry.Panel.BringToFront();
        UpdateMenuBrightness();
    }
}
