using Intersect.Client.Core;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
internal static class IntoxicationVisuals
{
    internal static long GreenUntil;
    internal static long DrunkUntil;
    internal static float BlurStrength => Math.Clamp((DrunkUntil-Environment.TickCount64)/1000f,0f,1f);
    private static readonly Dictionary<Guid,long> VomitAt = [];
    internal static void Apply(IntoxicationPacket packet)
    {
        var now = Environment.TickCount64;
        if (packet.GreenMilliseconds > 0) GreenUntil = Math.Max(GreenUntil,now+Math.Clamp(packet.GreenMilliseconds,0,120000));
        if (!packet.Vomit && packet.GreenMilliseconds>0) DrunkUntil=now+Math.Clamp(packet.GreenMilliseconds,0,120000);
        if (packet.Vomit) VomitAt[packet.PlayerId] = now;
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
    private void UpdateIntoxicationOverlay()
    {
        _intoxicationOverlay ??= new IntoxicationOverlay(GameCanvas) { MouseInputEnabled=false,KeyboardInputEnabled=false };
        _intoxicationOverlay.SetBounds(0,0,GameCanvas.Width,GameCanvas.Height);_intoxicationOverlay.SendToBack();
        _intoxicationOverlay.IsHidden=IntoxicationVisuals.GreenUntil<=Environment.TickCount64;
    }
}
