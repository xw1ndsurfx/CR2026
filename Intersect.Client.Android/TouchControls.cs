using Intersect.Client.Framework.Graphics;
using Intersect.Client.General;
using Intersect.Client.Core.Controls;
using Intersect.Client.Naval;
using Intersect.Client.Localization;
using Intersect.Client.MonoGame.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using XnaColor = Microsoft.Xna.Framework.Color;
using GameUi = Intersect.Client.Interface.Interface;

namespace Intersect.Client.Android;

internal static class TouchControls
{
    private readonly record struct TouchButton(Rectangle Bounds, Keys Key, string? Caption = null);
    private static readonly Dictionary<int, int> CapturedTouches = [];
    private static readonly HashSet<Keys> PressedKeys = [];
    private static int? _pointerId;
    private static Vector2 _pointer;
    private static bool _pointerIsUi;
    private static readonly Framework.Gwen.Input.TouchScrollGesture UiScroll = new();
    private static SpriteBatch? _batch;
    private static Texture2D? _pixel;
    private static Texture2D? _circle;
    private static readonly VirtualJoystick Joystick = new();
    private static Vector2 _joystickCenter;
    private static float _joystickRadius, _joystickTravel;
    private static TouchButton[] _buttons = [];
    private static Rectangle _layoutSize;
    private static bool InGame => Globals.GameState == GameStates.InGame;

    internal static Framework.GenericClasses.Rectangle FitHudControl(Framework.GenericClasses.Rectangle desired)
    {
        var scale = GameUi.MobileInputScale;
        var safe = MobileLayout.SafeBounds(TouchPanel.DisplayWidth, TouchPanel.DisplayHeight);
        Framework.GenericClasses.Rectangle Logical(Rectangle r) => new((int)(r.X / scale), (int)(r.Y / scale),
            Math.Max(1, (int)(r.Width / scale)), Math.Max(1, (int)(r.Height / scale)));
        var reserved = Buttons(TouchPanel.DisplayWidth, TouchPanel.DisplayHeight)
            .Where(button => button.Key != Keys.Escape).Select(button => Logical(button.Bounds)).ToList();
        reserved.Add(Logical(new Rectangle((int)(_joystickCenter.X - _joystickRadius), (int)(_joystickCenter.Y - _joystickRadius),
            (int)(_joystickRadius * 2), (int)(_joystickRadius * 2))));
        return MobileHudLayout.AvoidControls(desired, Logical(safe), reserved);
    }

    internal static bool TryGetSteeringDirection(out System.Numerics.Vector2 direction)
    {
        direction = Joystick.Current.Offset;
        return InGame && !GameUi.MobileWindowOpen && Joystick.FingerId != null &&
            (Joystick.Current.Horizontal != 0 || Joystick.Current.Vertical != 0);
    }

    private static TouchButton[] Buttons(int width, int height)
    {
        var safe = MobileLayout.SafeBounds(width, height);
        if (_layoutSize == safe) return _buttons;
        _layoutSize = safe;
        width = safe.Width; height = safe.Height;
        var size = Math.Clamp(height / 8, 72, 150);
        var margin = size / 4;
        Joystick.Cancel();
        _joystickCenter = new Vector2(safe.X + margin + size * 1.5f, safe.Y + height - margin - size * 1.5f);
        _joystickRadius = size * 1.2f;
        _joystickTravel = _joystickRadius - size * .42f;
        _buttons = [
            new(new(margin + size * 2, margin, size * 2, size * 3 / 4), Keys.Escape, "Menu"),
            new(new(width - margin - size * 3 / 2, height / 2, size * 3 / 2, size * 3 / 2), Keys.E, "Interact"),
            new(new(width - margin - size * 3, height / 2 + size / 2, size * 5 / 4, size), Keys.Q, "Block"),
            new(new(width - margin - size * 3, height / 2 - size * 3 / 4, size * 5 / 4, size), Keys.Space, "Pick up"),
        ];
        for (var i = 0; i < _buttons.Length; i++)
        {
            var bounds = _buttons[i].Bounds; bounds.Offset(safe.X, safe.Y);
            _buttons[i] = _buttons[i] with { Bounds = bounds };
        }
        return _buttons;
    }

    internal static void Poll(ref KeyboardState keyboard, ref MouseState mouse)
    {
        var touches = TouchPanel.GetState();
        var buttons = Buttons(TouchPanel.DisplayWidth, TouchPanel.DisplayHeight);
        var hideActions = InGame && GameUi.MobileWindowOpen;
        PressedKeys.Clear();
        foreach (var key in keyboard.GetPressedKeys()) PressedKeys.Add(key);
        var pointerDown = false;
        foreach (var touch in touches)
        {
            if (touch.State is TouchLocationState.Released or TouchLocationState.Invalid)
            {
                if (_pointerId == touch.Id && UiScroll.IsActive)
                {
                    _pointer = touch.Position;
                    if (touch.State == TouchLocationState.Released)
                    {
                        var scale = GameUi.MobileInputScale;
                        UiScroll.Move(new((int)(_pointer.X / scale), (int)(_pointer.Y / scale)));
                        pointerDown = UiScroll.End();
                    }
                    else UiScroll.End();
                }
                Joystick.Release(touch.Id);
                CapturedTouches.Remove(touch.Id);
                continue;
            }
            if (InGame && !hideActions && touch.State == TouchLocationState.Pressed &&
                Vector2.DistanceSquared(touch.Position, _joystickCenter) <= _joystickRadius * _joystickRadius &&
                Joystick.TryCapture(touch.Id))
                CapturedTouches[touch.Id] = -2;
            if (Joystick.FingerId == touch.Id)
            {
                Joystick.Update(touch.Id, new(touch.Position.X - _joystickCenter.X, touch.Position.Y - _joystickCenter.Y), _joystickTravel);
                continue;
            }
            if (CapturedTouches.GetValueOrDefault(touch.Id, -1) == -2) continue;
            if (InGame && touch.State == TouchLocationState.Pressed)
            {
                var button = Array.FindIndex(buttons, b => (!hideActions || b.Key == Keys.Escape && !GameUi.MobileFullscreenOpen) && b.Bounds.Contains(touch.Position));
                // A finger belongs to its original control until lifted; sliding onto the
                // world or another button never generates an accidental mouse click.
                CapturedTouches[touch.Id] = button;
                if (button == 0) GameUi.InGameUiOrNull?.GameMenu.ToggleMobileMenu();
            }
            if (InGame && CapturedTouches.TryGetValue(touch.Id, out var captured) && captured >= 0)
            {
                if (captured > 0 && !hideActions && buttons[captured].Bounds.Contains(touch.Position))
                    PressedKeys.Add(buttons[captured].Key);
                continue;
            }
            if (_pointerId == null && touch.State == TouchLocationState.Pressed)
            {
                _pointerId = touch.Id;
                _pointerIsUi = !InGame || GameUi.MobileTouchHitsInterface((int)touch.Position.X, (int)touch.Position.Y);
                if (InGame && _pointerIsUi)
                {
                    var scale = GameUi.MobileInputScale;
                    UiScroll.Begin(GameUi.MobileScrollTarget((int)touch.Position.X, (int)touch.Position.Y),
                        new((int)(touch.Position.X / scale), (int)(touch.Position.Y / scale)));
                }
            }
            if (_pointerId == touch.Id)
            {
                _pointer = touch.Position;
                if (UiScroll.IsActive)
                {
                    var scale = GameUi.MobileInputScale;
                    UiScroll.Move(new((int)(_pointer.X / scale), (int)(_pointer.Y / scale)));
                }
                else pointerDown = _pointerIsUi;
            }
        }
        if (!touches.Any(t => t.Id == _pointerId && t.State is not (TouchLocationState.Released or TouchLocationState.Invalid)))
        {
            _pointerId = null;
            // Some Android focus changes drop the release event entirely.
            if (UiScroll.IsActive) UiScroll.End();
        }
        if (!InGame || GameUi.MobileWindowOpen || !touches.Any(t => t.Id == Joystick.FingerId &&
            t.State is not (TouchLocationState.Released or TouchLocationState.Invalid))) Joystick.Cancel();
        if (!InGame) CapturedTouches.Clear();
        if (Joystick.Current.Horizontal < 0) PressedKeys.Add(Keys.Left);
        if (Joystick.Current.Horizontal > 0) PressedKeys.Add(Keys.Right);
        if (Joystick.Current.Vertical < 0) PressedKeys.Add(Keys.Up);
        if (Joystick.Current.Vertical > 0) PressedKeys.Add(Keys.Down);
        keyboard = new KeyboardState(PressedKeys.ToArray());
        mouse = new MouseState((int)_pointer.X, (int)_pointer.Y, 0,
            pointerDown ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
    }

    internal static void Draw(GraphicsDevice device)
    {
        if (!InGame) return;
        if (_pixel == null || _pixel.GraphicsDevice != device || _pixel.IsDisposed)
        {
            _batch?.Dispose(); _pixel?.Dispose(); _circle?.Dispose();
            _batch = new SpriteBatch(device);
            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData([XnaColor.White]);
            _circle = new Texture2D(device, 128, 128);
            var circlePixels = new XnaColor[128 * 128];
            for (var y = 0; y < 128; y++)
                for (var x = 0; x < 128; x++)
                    circlePixels[y * 128 + x] = Vector2.DistanceSquared(new(x + .5f, y + .5f), new(64, 64)) <= 64 * 64
                        ? XnaColor.White : XnaColor.Transparent;
            _circle.SetData(circlePixels);
        }
        var hideActions = GameUi.MobileWindowOpen;
        var buttons = Buttons(device.Viewport.Width, device.Viewport.Height);
        _batch!.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
        if (!hideActions) DrawJoystick();
        foreach (var button in buttons)
        {
            var (bounds, key, caption) = button;
            if (hideActions && (key != Keys.Escape || GameUi.MobileFullscreenOpen)) continue;
            var pressed = key == Keys.Escape ? GameUi.InGameUiOrNull?.GameMenu.IsHidden == false : PressedKeys.Contains(key);
            _batch.Draw(_pixel, bounds, pressed ? new XnaColor(108, 76, 35, 230) : new XnaColor(20, 28, 40, 210));
            _batch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 3), XnaColor.Gold);
            if (caption != null)
            {
                var label = caption == "Pick up" && NavalWorld.OwnShip is { } ship && NavalWorld.Driver(ship) == Globals.Me?.Id
                    ? "Anchor" : caption;
                DrawCaption(ClientLanguages.Text(label), bounds);
                continue;
            }
            var center = bounds.Center.ToVector2();
            var length = bounds.Width * .45f;
            var angle = key switch { Keys.Up => -MathHelper.PiOver2, Keys.Down => MathHelper.PiOver2, Keys.Left => MathHelper.Pi, _ => 0f };
            Line(center - new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * length / 2, length, angle);
            var end = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * length / 2;
            Line(end, length / 2, angle + MathHelper.Pi * .75f);
            Line(end, length / 2, angle - MathHelper.Pi * .75f);
        }
        _batch.End();
    }

    private static void DrawJoystick()
    {
        Circle(_joystickCenter, _joystickRadius, new XnaColor(195, 161, 79, 170));
        Circle(_joystickCenter, _joystickRadius - 4, new XnaColor(20, 28, 40, 220));
        var offset = Joystick.Current.Offset;
        var thumb = _joystickCenter + new Vector2(offset.X, offset.Y) * _joystickTravel;
        Circle(thumb, _joystickRadius - _joystickTravel,
            Joystick.FingerId != null ? new XnaColor(235, 204, 115, 245) : new XnaColor(222, 227, 235, 190));
    }

    private static void Circle(Vector2 center, float radius, XnaColor color) =>
        _batch!.Draw(_circle!, new Rectangle((int)(center.X - radius), (int)(center.Y - radius),
            (int)(radius * 2), (int)(radius * 2)), color);

    private static void DrawCaption(string text, Rectangle bounds)
    {
        if (Core.Graphics.UIFont is not Font<SpriteFont> font) return;
        var spriteFont = font.GetRendererFor(Math.Max(10, Core.Graphics.UIFontSize)).PlatformObject;
        var measured = spriteFont.MeasureString(text);
        if (measured.X <= 0 || measured.Y <= 0) return;
        var scale = Math.Min((bounds.Width - 20f) / measured.X, bounds.Height * .30f / measured.Y);
        var position = bounds.Center.ToVector2() - measured * scale / 2;
        _batch!.DrawString(spriteFont, text, position, XnaColor.White, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
    }

    private static void Line(Vector2 start, float length, float angle) =>
        _batch!.Draw(_pixel!, start, null, XnaColor.White, angle, Vector2.Zero, new Vector2(length, 5), SpriteEffects.None, 0);
}
