using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.Mounts;
using Rect = Intersect.Client.Framework.GenericClasses.Rectangle;
namespace Intersect.Client.Interface.Game;

internal sealed class JoustViewport(Base parent) : Base(parent)
{
    internal JoustView? State;
    internal bool ReadOnly;
    private long _received;
    internal void Apply(JoustView? state) { State = state; _received = Environment.TickCount64; }
    internal JoustRules Rules = new();
    internal double AimX = .5;
    internal double AimY = .75;
    private readonly MountAnimationClock _clock = new();
    protected override void OnMouseMoved(int x, int y, int dx, int dy)
    {
        if (ReadOnly) return;
        var p = CanvasPosToLocal(new(x, y));
        AimX = Math.Clamp((p.X - (Width - 256) / 2d) / 256, 0, 1);
        AimY = Math.Clamp((p.Y - 20) / 256d, 0, 1);
        base.OnMouseMoved(x, y, dx, dy);
    }
    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        var renderer = skin.Renderer;
        renderer.DrawColor = new Color(255, 28, 25, 35); renderer.DrawFilledRect(new Rect(0, 0, Width, Height));
        if (State == null) return;
        renderer.DrawColor = Color.White;
        var aiming = ReadOnly || State.Phase is JoustPhase.Aim or JoustPhase.Impact or JoustPhase.Results;
        var remaining = ReadOnly ? Math.Max(0, State.RemainingMs - (int)Math.Min(int.MaxValue, Environment.TickCount64 - _received)) : State.RemainingMs;
        var sheet = Globals.ContentManager?.GetTexture(TextureType.Image, aiming ? (State.IsPve && State.AiOpponent == JoustAiOpponent.Night ? "jousting/knight-closeup-black.png" : "jousting/knight-closeup.png") : JoustMountPreset.Opponent(State.AiOpponent).Loop);
        if (sheet != null)
        {
            var frame = ReadOnly ? Math.Max(0, State.PhaseDurationMs - remaining) / 100 % 4 : (int)(Environment.TickCount64 / 100 % 4);
            if (aiming)
            {
                var row = State.Phase is JoustPhase.Impact or JoustPhase.Results && State.PlayerStrike.Hit != JoustHit.Miss ? 3 : 0;
                if (row == 3) frame = State.PlayerStrike.Hit == JoustHit.Unhorsed ? 2 : State.PlayerStrike.BrokenLance ? 1 : 0;
                if (row == 3 && State.Phase == JoustPhase.Results && State.PlayerStrike.Hit == JoustHit.Unhorsed) frame = 3;
                var progress = 1 - Math.Clamp(remaining / (double)Math.Max(1, State.PhaseDurationMs), 0, 1);
                var size = ReadOnly && State.Phase == JoustPhase.Charge ? 96 + (int)(160 * progress) : 256;
                renderer.DrawTexturedRect(sheet, new Rect((Width - size) / 2, 276 - size, size, size), Color.White,
                    frame / 4f, row / 4f, (frame + 1) / 4f, (row + 1) / 4f);
            }
            else
            {
                var def = new MountDefinition { LoopFrames = sheet.Width / 128 };
                var sample = _clock.Sample(def, State.Phase == JoustPhase.Charge, Environment.TickCount64);
                frame = sample.Frame % def.LoopFrames;
                var progress = 1 - Math.Clamp(State.RemainingMs / (double)Math.Max(1, State.PhaseDurationMs), 0, 1);
                var x = 24 + (int)(progress * (Width / 2 - 150));
                renderer.DrawTexturedRect(sheet, new Rect(x, 120, 128, 128), Color.White, frame * 128f / sheet.Width, .5f, (frame + 1) * 128f / sheet.Width, .75f);
                renderer.DrawTexturedRect(sheet, new Rect(Width - x - 128, 120, 128, 128), Color.White, frame * 128f / sheet.Width, .25f, (frame + 1) * 128f / sheet.Width, .5f);
            }
        }
        if (ReadOnly && (State.Phase is JoustPhase.Charge or JoustPhase.Aim or JoustPhase.Impact) &&
            !(State.Phase == JoustPhase.Impact && State.PlayerStrike.BrokenLance))
        {
            var lance = Globals.ContentManager?.GetTexture(TextureType.Image, "jousting/lance-foreground.png");
            if (lance != null) renderer.DrawTexturedRect(lance, new Rect(Width / 2 - 186, 186, 192, 192), Color.White);
        }
        if (!ReadOnly && State.Phase == JoustPhase.Aim)
        {
            void Target(JoustTarget target, Color color)
            {
                renderer.DrawColor = color;
                renderer.DrawLinedRect(new Rect((Width - 256) / 2 + (int)(target.Left * 256), 20 + (int)(target.Top * 256),
                    (int)((target.Right - target.Left) * 256), (int)((target.Bottom - target.Top) * 256)));
            }
            Target(Rules.BodyTarget, Color.Green); Target(Rules.ShieldTarget, Color.White);
            // Show both the pointer's requested aim and the authoritative lance position.
            var x = (Width - 256) / 2 + (int)(State.Aim.X * 256); var y = 20 + (int)(State.Aim.Y * 256);
            var lance = Globals.ContentManager?.GetTexture(TextureType.Image, "jousting/lance-foreground.png");
            if (lance != null) renderer.DrawTexturedRect(lance, new Rect(x - 186, y - 6, 192, 192), Color.White);
            renderer.DrawColor = Color.White; renderer.DrawLinedRect(new Rect(x - 10, y - 10, 20, 20));
            renderer.DrawLine(x - 18, y, x + 18, y); renderer.DrawLine(x, y - 18, x, y + 18);
            renderer.DrawColor = Color.Red; renderer.DrawLinedRect(new Rect((Width - 256) / 2 + (int)(AimX * 256) - 2, 20 + (int)(AimY * 256) - 2, 4, 4));
        }
        renderer.DrawColor = Color.Green;
        renderer.DrawFilledRect(new Rect(20, Height - 20, (int)((Width - 40) * Math.Clamp(remaining / (double)Math.Max(1, State.PhaseDurationMs), 0, 1)), 8));
    }
}
