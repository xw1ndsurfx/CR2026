using System.Runtime.CompilerServices;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.ControlInternal;
using Intersect.Client.General;
using Intersect.Client.Localization;
namespace Intersect.Client.Interface.Game;
internal static class HudPlacement
{
    internal static bool IsCompactTextHud(Base control) => control is DayPhaseHud or ExperienceEventHud;

    internal static void FitTextHud(Label label, int defaultX, int defaultY)
    {
        var size = label.Skin.Renderer.MeasureText(label.Font ?? label.Skin.DefaultFont, label.FontSize, label.Text);
        label.Padding = new Intersect.Client.Framework.Gwen.Padding(4, 2, 26, 2);
        label.TextAlign = Intersect.Client.Framework.Gwen.Pos.Left | Intersect.Client.Framework.Gwen.Pos.CenterV;
        label.SetSize(Math.Min(size.X + 30, Math.Max(1, (label.Parent?.Width ?? size.X + 46) - 16)),
            Math.Max(16, size.Y + 4));
        if (!HasSavedPosition(label)) label.SetPosition(defaultX, defaultY);
        label.SetPosition(Math.Clamp(label.X, 0, Math.Max(0, (label.Parent?.Width ?? label.Width) - label.Width)),
            Math.Clamp(label.Y, 0, Math.Max(0, (label.Parent?.Height ?? label.Height) - label.Height)));
        if (Positions.TryGetValue(label, out var state) && state.Handle is { } handle) ArrangeGrip(label, handle);
    }

    private static void ArrangeGrip(Base control, Grip handle) => handle.SetBounds(
        Math.Max(0, control.Width - (IsCompactTextHud(control) ? 24 : 54)), 2, 22, Math.Min(14, Math.Max(1, control.Height - 4)));
    private sealed class Position
    {
        internal bool Moved;
        internal int X,Y;
        internal Grip? Handle;
    }
    private static readonly ConditionalWeakTable<Base,Position> Positions=new();
    private sealed class Grip:Dragger
    {
        internal Grip(Base panel):base(panel,"HudMoveGrip")
        {
            _target=panel;KeyboardInputEnabled=false;
            SetToolTipText(ClientLanguages.Text("Drag to move this HUD"));
        }
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            skin.Renderer.DrawColor=new Color(190,224,190,113);
            for(var i=0;i<3;i++)skin.Renderer.DrawFilledRect(new Intersect.Client.Framework.GenericClasses.Rectangle(3+i*5,6,2,3));
        }
    }
    private static bool IsHud(Base control)=>control.Name=="MinimapHud" ||
        control.Name is "PlayerBox" or "TargetBox" or "HotBarWindow" or "HotbarWindow" or "PlayerStatusWindow" or "PartyRosterHud" or "DayPhaseHud" ||
        control.GetType().Name.EndsWith("Hud",StringComparison.Ordinal) || control.GetType().Name=="ExperienceEventHud";
    internal static bool HasSavedPosition(Base control)=>Positions.TryGetValue(control,out var position) && position.Moved;
    internal static void Update(Canvas canvas)
    {
        foreach(var control in canvas.Children.Where(IsHud).ToArray())
        {
            if(string.IsNullOrWhiteSpace(control.Name))continue;
            var state=Positions.GetValue(control,panel=>
            {
                var position=new Position();
                var saved=Globals.Database?.LoadPreference("HudPosition:"+panel.Name,"")??"";
                var parts=saved.Split(',');
                if(parts.Length==2 && int.TryParse(parts[0],out var x) && int.TryParse(parts[1],out var y))
                {position.Moved=true;position.X=x;position.Y=y;}
                var grip=new Grip(panel);position.Handle=grip;
                grip.Dragged+=(_,_)=>
                {
                    position.Moved=true;position.X=panel.X;position.Y=panel.Y;
                    Globals.Database?.SavePreference("HudPosition:"+panel.Name,$"{panel.X},{panel.Y}");
                };
                return position;
            });
            ArrangeGrip(control, state.Handle!);
            state.Handle.BringToFront();
            if(state.Moved)
            {
                control.Alignment=[];control.Dock=Intersect.Client.Framework.Gwen.Pos.None;
                control.SetPosition(Math.Clamp(state.X,0,Math.Max(0,canvas.Width-control.Width)),Math.Clamp(state.Y,0,Math.Max(0,canvas.Height-control.Height)));
            }
        }
    }
}
