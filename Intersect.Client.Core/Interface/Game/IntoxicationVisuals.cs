using Intersect.Client.Core;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Network.Packets.Server;
using Intersect.Enums;
namespace Intersect.Client.Interface.Game;
internal static class IntoxicationVisuals
{
    internal static long GreenUntil;
    internal static long DrunkUntil;
    internal static int DrunkIntensity;
    internal static int RemainingSeconds
    {
        get
        {
            var remainingMs = Math.Max(0L, DrunkUntil - Environment.TickCount64);
            return (int)Math.Ceiling(remainingMs / 1000d);
        }
    }
    internal static float BlurStrength => DrunkUntil > Environment.TickCount64 ? DrunkIntensity / 4f : 0f;
    private static readonly Dictionary<Guid,long> VomitAt = [];
    internal static void Apply(IntoxicationPacket packet)
    {
        var now = Environment.TickCount64;
        var duration = Math.Clamp(packet.GreenMilliseconds, 0, 120000);
        if (!packet.Vomit)
        {
            // Server state packets can shorten intoxication as a dose wears off.
            // Do not keep an older, longer green overlay after recovery.
            GreenUntil = now + duration;
            DrunkUntil = now + duration;
            DrunkIntensity = duration > 0 ? Math.Clamp(packet.Intensity, 0, 4) : 0;
        }
        else
        {
            if (duration > 0) GreenUntil = Math.Max(GreenUntil, now + duration);
            VomitAt[packet.PlayerId] = now;
        }
    }
    private static readonly Direction[] Compass =
    [
        Direction.Up, Direction.UpRight, Direction.Right, Direction.DownRight,
        Direction.Down, Direction.DownLeft, Direction.Left, Direction.UpLeft,
    ];

    internal static Direction DistortMove(Direction intended)
    {
        if (DrunkUntil <= Environment.TickCount64 || DrunkIntensity <= 0)
        {
            return intended;
        }

        var index = Array.IndexOf(Compass, intended);
        var chancePercent = Math.Clamp(DrunkIntensity * 12, 0, 48);
        if (index < 0 || Random.Shared.Next(100) >= chancePercent)
        {
            return intended;
        }

        var offset = Random.Shared.Next(2) == 0 ? -1 : 1;
        return Compass[(index + offset + Compass.Length) % Compass.Length];
    }

    internal static void DrawVomit(Guid id, System.Numerics.Vector2 origin)
    {
        if (!VomitAt.TryGetValue(id,out var at)) return;
        var elapsed = Environment.TickCount64-at;
        if (elapsed > 1400) { VomitAt.Remove(id); return; }
        for (int i=0;i<12;i++)
        {
            var age = (elapsed-i*65)/850f;
            if (age is < 0 or > 1) continue;
            Graphics.DrawGameTexture(Graphics.Renderer.WhitePixel,new FloatRect(0,0,1,1),
                new FloatRect(origin.X+age*22-3,origin.Y-33+age*age*30,4,4),new Color((int)(220*(1-age)),100,163,35));
        }
    }
}
internal sealed class IntoxicationOverlay(Base parent) : Base(parent, nameof(IntoxicationOverlay))
{
    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        var left=IntoxicationVisuals.GreenUntil-Environment.TickCount64;
        if(left<=0)return;
        var alpha=(int)(82*Math.Min(1,left/1000f));
        skin.Renderer.DrawColor=new Color(alpha,73,132,20); skin.Renderer.DrawFilledRect(RenderBounds);
        // Short falling streaks make the splash read as liquid while it fades.
        for(int i=0;i<9;i++)
        {
            var x=(int)(Width*(i+.5f)/9);var h=(int)(Height*(.08f+.045f*(i%4)));
            skin.Renderer.DrawColor=new Color(alpha/2,92,163,28);skin.Renderer.DrawFilledRect(new Rectangle(x,0,Math.Max(8,Width/55),h));
        }
    }
}
public partial class GameInterface
{
    private IntoxicationOverlay? _intoxicationOverlay;
    private IntoxicationStatusHud? _intoxicationHud;
    private void UpdateIntoxicationOverlay()
    {
        _intoxicationOverlay ??= new IntoxicationOverlay(GameCanvas) { MouseInputEnabled=false,KeyboardInputEnabled=false };
        _intoxicationOverlay.SetBounds(0,0,GameCanvas.Width,GameCanvas.Height);_intoxicationOverlay.SendToBack();
        _intoxicationOverlay.IsHidden = IntoxicationVisuals.GreenUntil <= Environment.TickCount64;
        _intoxicationHud ??= new IntoxicationStatusHud(GameCanvas);
        _intoxicationHud.Update(this);
    }
}
