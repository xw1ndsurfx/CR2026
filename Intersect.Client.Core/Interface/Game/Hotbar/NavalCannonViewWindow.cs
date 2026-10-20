using Microsoft.Extensions.Logging;
using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Naval;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game.Hotbar;

/// <summary>Opt-in, bounded perspective preview. The native world renderer and server remain authoritative.</summary>
internal sealed class NavalCannonViewWindow : Window
{
    private readonly Viewport _viewport;
    private readonly Label _side,_limit,_hint;
    private readonly Button _port,_starboard,_fire,_close;
    private Guid _ship;
    internal NavalCannonViewWindow(Base parent):base(parent,T("Cannon view"),false,nameof(NavalCannonViewWindow))
    {
        Alignment=[Alignments.Center];DeleteOnClose=false;DisableResizing();
        Closed+=(_,_)=>NavalWorld.CannonViewOpen=false;
        _viewport=new Viewport(this);
        _side=Caption("CannonSide");_limit=Caption("CannonVisibilityLimit");_hint=Caption("CannonViewHint");
        _port=Action("PortView",()=>NavalWorld.CannonViewStarboard=false);
        _starboard=Action("StarboardView",()=>NavalWorld.CannonViewStarboard=true);
        _fire=Action("FireBroadside",()=>NavalWorld.ActivateSlot(NavalWorld.CannonViewStarboard?1:0));
        _close=Action("CloseCannonView",()=>NavalWorld.CannonViewOpen=false);
        Hide();
    }
    protected override void EnsureInitialized() { }
    private static string T(string source)=>NavalSetupText.Get(source,ClientLanguages.Current);
    private Label Caption(string id)=>new(this,id) {AutoSizeToContents=false,
        Font=GameContentManager.Current.GetFont("sourcesanspro")??Skin.DefaultFont,FontSize=11,
        TextAlign=Pos.Center,TextColorOverride=new Color(255,240,220,165),MouseInputEnabled=false};
    private Button Action(string id,Action action)
    {
        var button=new Button(this,id) {Font=GameContentManager.Current.GetFont("sourcesansproblack")??Skin.DefaultFont,FontSize=10};
        button.Clicked+=(_,_)=>action();return button;
    }
    internal void Update(ShipSnapshot? ship,bool open)
    {
        if(!open || ship is not {Sinking:false})
        {NavalWorld.CannonViewOpen=false;_ship=Guid.Empty;Hide();_viewport.Release();return;}
        if(_ship!=ship.Id) {_ship=ship.Id;NavalWorld.CannonViewStarboard=false;_viewport.Release();}
        var width=Math.Min(880,Math.Max(300,(Parent?.Width??904)-24));
        var height=Math.Min(640,Math.Max(330,(Parent?.Height??672)-32));
        SetSize(width,height);
        var pictureHeight=Math.Min((width-32)*9/16,height-175);
        var pictureWidth=pictureHeight*16/9;
        _side.SetBounds(16,30,width-32,26);
        _viewport.SetBounds((width-pictureWidth)/2,60,pictureWidth,pictureHeight);
        _limit.SetBounds(16,65+pictureHeight,width-32,23);
        _hint.SetBounds(16,88+pictureHeight,width-32,23);
        var buttons=new[]{_port,_starboard,_fire,_close};var bw=(width-50)/4;
        for(var i=0;i<buttons.Length;i++) buttons[i].SetBounds(16+i*(bw+6),height-51,bw,30);
        Title=T("Cannon view");_port.Text=T("Port");_starboard.Text=T("Starboard");_close.Text=T("Close");
        _side.Text=T(NavalWorld.CannonViewStarboard?"Starboard cannon view":"Port cannon view");
        EventPanelText.Fit(_limit,T("Real loaded maps only · Terrain has no height data"),11,8);
        EventPanelText.Fit(_hint,T("Shots follow the selected broadside · The captain keeps control"),11,8);
        var slot=NavalWorld.CannonViewStarboard?1:0;var cooldown=NavalWorld.Cooldown(slot);
        _fire.Text=cooldown>0?string.Format(T("Reload: {0}s"),(int)Math.Ceiling(cooldown/1000d)):T("Fire broadside");
        _fire.IsDisabled=ship.CaptainId!=Globals.Me?.Id || !ship.CannonsEnabled || cooldown>0 || Globals.Me?.IsBusy!=false;
        _fire.TooltipText=T("Only the captain can fire the broadside in this preview.");
        if(IsHidden) {Show();BringToFront();}
    }
    private sealed class Viewport(Base parent):Base(parent,"CannonPerspectiveViewport")
    {
        private IGameRenderTexture? _texture;
        private long _nextFrame;
        private bool _failed;
        internal void Release() {_texture?.Dispose();_texture=null;_nextFrame=0;_failed=false;}
        protected override void Dispose(bool disposing) {if(disposing) Release();base.Dispose(disposing);}
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var draw=skin.Renderer;var previous=draw.DrawColor;
            draw.DrawColor=new Color(255,27,40,53);draw.DrawFilledRect(new Rectangle(0,0,Width,Height));
            if(!_failed && Graphics.Renderer is {} renderer && Environment.TickCount64>=_nextFrame)
            {
                _nextFrame=Environment.TickCount64+50; // 480x270 at <=20 Hz, no CPU texture readback.
                try
                {
                    if(NavalWorld.CannonScene() is {} scene)
                    {
                        _texture??=renderer.CreateRenderTexture(480,270);
                        _failed=!renderer.RenderMapPerspective(_texture,scene);
                    }
                    else {_texture?.Dispose();_texture=null;} // Map unloading must not leave an old island frozen in view.
                }
                catch(Exception e) when(e is InvalidOperationException or NotSupportedException or ArgumentException)
                {
                    _failed=true;
                    Intersect.Core.ApplicationContext.CurrentContext.Logger.LogWarning(e,"Naval perspective preview disabled; ordinary world view preserved");
                }
                if(_failed) {_texture?.Dispose();_texture=null;}
            }
            if(_texture!=null) draw.DrawTexturedRect(_texture,new Rectangle(0,0,Width,Height),Color.White);
            if(_failed)
            {
                draw.DrawColor=new Color(255,240,220,165);
                draw.RenderText(GameContentManager.Current.GetFont("sourcesanspro")??Skin.DefaultFont,11,
                    new Point(8,Height/2),T("Perspective preview unavailable on this renderer."));
            }
            else
            {
                draw.DrawColor=new Color(210,240,220,165);
                draw.DrawFilledRect(new Rectangle(Width/2-10,Height/2,21,1));
                draw.DrawFilledRect(new Rectangle(Width/2,Height/2-10,1,21));
            }
            draw.DrawColor=previous;
        }
    }
}
