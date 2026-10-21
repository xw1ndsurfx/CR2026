using Microsoft.Extensions.Logging;
using Intersect.Client.Framework.Input;
using Intersect.Client.Framework.Content;
using Intersect.Configuration;
using Newtonsoft.Json;
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
    private readonly Button _port,_starboard,_fire,_close,_ammo;
    private readonly NavalSailingBar _gunnery;
    private bool _crewWasOpen;
    private Guid _ship;
    private bool _fullscreen;
    // Keep the world image below canvas-level HUDs, including after clicking the viewport.
    public override void Touch() {if(_fullscreen) SendToBack();else base.Touch();}
    protected override void Layout(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        if(!_fullscreen) {base.Layout(skin);return;}
        InnerPanel.Dock=Pos.None;InnerPanel.SetBounds(0,0,Width,Height);
    }
    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {if(!_fullscreen) base.Render(skin);}
    internal static Point PerspectiveResolution(int width,int height,bool fullscreen)
    {
        width=Math.Max(1,width);height=Math.Max(1,height);
        var scale=Math.Min(1,Math.Min((fullscreen?960d:480)/width,(fullscreen?540d:270)/height));
        return new Point(Math.Max(1,(int)Math.Round(width*scale)),Math.Max(1,(int)Math.Round(height*scale)));
    }
    internal NavalCannonViewWindow(Base parent):base(parent,T("Cannon view"),false,nameof(NavalCannonViewWindow))
    {
        Alignment=[Alignments.Center];DeleteOnClose=false;DisableResizing();
        Closed+=(_,_)=>NavalWorld.CannonViewOpen=false;
        _viewport=new Viewport(this);
        _side=Caption("CannonSide");_limit=Caption("CannonVisibilityLimit");_hint=Caption("CannonViewHint");
        _port=Action("PortView",()=>ChooseSide(false));
        _starboard=Action("StarboardView",()=>ChooseSide(true));
        _fire=Action("FireBroadside",()=>
        {
            if(_viewport.CrewMode) NavalWorld.RequestStation(NavalWorld.Gunner.LeaseId==Guid.Empty?CannonStationAction.Acquire:CannonStationAction.Fire);
            else NavalWorld.ActivateSlot(NavalWorld.CannonViewStarboard?1:0);
        });
        _ammo=Action("CannonAmmunition",()=>NavalWorld.SelectedAmmo=(CannonAmmoKind)(((int)NavalWorld.SelectedAmmo+1)%3));
        _gunnery=new NavalSailingBar(this,"GunneryExperience") {AutoSizeToContents=false,
            Font=GameContentManager.Current.GetFont("sourcesansproblack")??Skin.DefaultFont,FontSize=10,TextAlign=Pos.Center,MouseInputEnabled=false};
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
    private void ChooseSide(bool starboard)
    {
        NavalWorld.CannonViewStarboard=starboard;
        if(_viewport.CrewMode && NavalWorld.Gunner.LeaseId!=Guid.Empty) NavalWorld.RequestStation(CannonStationAction.Acquire);
    }
    internal void Update(ShipSnapshot? ship,bool open)
    {
        if(!open || ship is not {Sinking:false})
        {
            if(_crewWasOpen) NavalWorld.RequestStation(CannonStationAction.Release);
            _crewWasOpen=false;NavalWorld.CannonViewOpen=false;_ship=Guid.Empty;Hide();_viewport.Release();return;
        }
        if(_ship!=ship.Id) {_ship=ship.Id;NavalWorld.CannonViewStarboard=false;_viewport.Release();}
        var crew=ship.CaptainId!=Globals.Me?.Id;
        _viewport.CrewMode=crew;_crewWasOpen=crew;
        _fullscreen=crew;DrawShadow=!crew;Titlebar.IsHidden=crew;
        Alignment=crew?[]:[Alignments.Center];
        InnerPanel.Dock=crew?Pos.None:Pos.Fill;
        if(crew) NavalWorld.RenewStation();
        var width=crew?Math.Max(1,Parent?.Width??800):Math.Min(880,Math.Max(300,(Parent?.Width??904)-24));
        var height=crew?Math.Max(1,Parent?.Height??600):Math.Min(640,Math.Max(330,(Parent?.Height??672)-32));
        if(crew) {SetBounds(0,0,width,height);InnerPanel.SetBounds(0,0,width,height);}else SetSize(width,height);
        var pictureHeight=Math.Min((width-32)*9/16,height-(crew?240:175));
        var pictureWidth=pictureHeight*16/9;
        _side.SetBounds(16,30,width-32,26);
        _viewport.SetBounds((width-pictureWidth)/2,60,pictureWidth,pictureHeight);
        _limit.SetBounds(16,65+pictureHeight,width-32,23);
        _hint.SetBounds(16,88+pictureHeight,width-32,23);
        _ammo.IsHidden=_gunnery.IsHidden=!crew;
        _ammo.SetBounds(16,height-117,width-32,30);_gunnery.SetBounds(16,height-82,width-32,22);
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
        if(crew)
        {
            var gun=NavalWorld.Gunner;var selected=gun.Ammunition.FirstOrDefault(a=>a.Kind==NavalWorld.SelectedAmmo);
            _side.Text=gun.LeaseId==Guid.Empty?T("Crew cannon station"):string.Format(T("Cannon {0} · {1}"),gun.Station+1,T(gun.Starboard?"Starboard":"Port"));
            _gunnery.Value=gun.Required<=0?1:(float)Math.Clamp(gun.Experience/(double)gun.Required,0,1);
            _gunnery.Text=gun.Required<=0?string.Format(T("Gunnery · Level {0} · MAX"),gun.Level):string.Format(T("Gunnery · Level {0} · {1}/{2} EXP"),gun.Level,gun.Experience,gun.Required);
            var qty=selected?.Quantity??0;
            EventPanelText.Fit(_ammo,string.Format(T("{0} · Level {1} · {2}"),T(CannonAmmunition.Caption(NavalWorld.SelectedAmmo)),selected?.RequiredLevel??1,
                qty<0?T("Free"):string.Format(T("{0} shots"),qty)),10,8);
            _ammo.TooltipText=selected==null?T("Ammunition unavailable."):string.Format(T("Damage {0}% · Range {1}% · Reload {2}%"),selected.DamagePercent,selected.RangePercent,selected.ReloadPercent);
            _fire.Text=gun.LeaseId==Guid.Empty?T("Take cannon"):gun.ReloadMilliseconds>0?string.Format(T("Reload: {0}s"),(int)Math.Ceiling(gun.ReloadMilliseconds/1000d)):T("Fire cannon");
            _fire.IsDisabled=!gun.Enabled || Globals.Me?.IsBusy!=false || gun.LeaseId!=Guid.Empty &&
                (gun.ReloadMilliseconds>0 || selected?.Available!=true || gun.Starboard!=NavalWorld.CannonViewStarboard);
            _fire.TooltipText=T("Move the mouse to aim · Left click to fire");
            EventPanelText.Fit(_hint,T(selected!=null && gun.Level<selected.RequiredLevel?"This ammunition requires a higher Gunnery level.":
                selected?.Available!=true?"Not enough ammunition for this cannonball.":"Move the mouse to aim · Left click to fire"),11,8);
            if(!string.IsNullOrEmpty(gun.Message)) EventPanelText.Fit(_limit,T(gun.Message),11,8);
        }
        if(crew)
        {
            _viewport.SetBounds(0,0,width,height);_viewport.SendToBack();
            _side.SetBounds(16,102,width-32,25);
            _limit.SetBounds(16,130,width-32,23);
            _limit.IsHidden=string.IsNullOrEmpty(NavalWorld.Gunner.Message);
            _hint.SetBounds(16,Math.Max(158,height-212),width-32,23);
            var commandWidth=Math.Min(640,width-32);var commandX=(width-commandWidth)/2;var buttonWidth=(commandWidth-18)/4;
            for(var i=0;i<buttons.Length;i++) buttons[i].SetBounds(commandX+i*(buttonWidth+6),height-180,buttonWidth,30);
            // Ammo selection and Gunnery progress are already available in the native hotbar below.
            _ammo.Hide();_gunnery.Hide();
        }
        else _limit.Show();
        if(IsHidden) Show();
        if(crew) SendToBack();
    }
    private sealed class Viewport(Base parent):Base(parent,"CannonPerspectiveViewport")
    {
        internal bool CrewMode {get;set;}
        private Point _aim;
        private string _artName="";
        private CannonStationArtwork? _art;
        protected override void OnMouseMoved(int x,int y,int dx,int dy)
        {
            base.OnMouseMoved(x,y,dx,dy);if(!CrewMode || Width<1 || Height<1) return;
            var p=CanvasPosToLocal(new Point(x,y));
            var aim=GunneryAim.FromViewport(2*p.X/(double)Width-1,1-2*p.Y/(double)Height,Width/(double)Height,NavalWorld.Gunner.AimArcDegrees);
            NavalWorld.GunYaw=aim.Yaw;NavalWorld.GunElevation=aim.Elevation;
            var projected=GunneryAim.ToViewport(aim.Yaw,aim.Elevation,Width/(double)Height);
            _aim=new Point((int)((projected.X+1)*Width/2),(int)((1-projected.Y)*Height/2));
        }
        protected override void OnMouseDown(MouseButton button,Point position,bool userAction=true)
        {
            base.OnMouseDown(button,position,userAction);
            if(CrewMode && button==MouseButton.Left && NavalWorld.Gunner.Ammunition.FirstOrDefault(a=>a.Kind==NavalWorld.SelectedAmmo)?.Available==true)
                NavalWorld.RequestStation(CannonStationAction.Fire);
        }
        private void DrawCannon(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var filename=NavalWorld.Presentation.CrewCannonGraphic;
            if(!CrewMode || !NavalPresentation.SafeAsset(filename) || Globals.ContentManager?.GetTexture(TextureType.Ocean,filename) is not {} texture) return;
            if(_artName!=filename)
            {
                _artName=filename;_art=null;
                try
                {
                    var path=Path.Combine(ClientConfiguration.ResourcesDirectory,"ocean",filename+".crew.json");
                    if(File.Exists(path) && new FileInfo(path).Length<=16384)
                        _art=JsonConvert.DeserializeObject<CannonStationArtwork>(File.ReadAllText(path),new JsonSerializerSettings {MaxDepth=8,TypeNameHandling=TypeNameHandling.None});
                }
                catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) {_art=null;}
            }
            if(_art?.IsValid(texture.Width,texture.Height)!=true) return;
            var frame=_art.Pose(NavalWorld.GunYaw);var w=Math.Min((int)(Width*.6),(int)(Height*.7));var h=w*frame.Height/frame.Width;
            var offset=(int)(NavalWorld.GunYaw/NavalWorld.Gunner.AimArcDegrees*8);
            skin.Renderer.DrawTexturedRect(texture,new Rectangle((Width-w)/2+offset,Height-h,w,h),Color.White,
                frame.X/(float)texture.Width,frame.Y/(float)texture.Height,(frame.X+frame.Width)/(float)texture.Width,(frame.Y+frame.Height)/(float)texture.Height);
        }
        private IGameRenderTexture? _texture;
        private long _nextFrame;
        private bool _failed;
        internal void Release() {_texture?.Dispose();_texture=null;_nextFrame=0;_failed=false;_aim=default;NavalWorld.GunYaw=0;NavalWorld.GunElevation=-Math.Atan(.12)*180/Math.PI;}
        protected override void Dispose(bool disposing) {if(disposing) Release();base.Dispose(disposing);}
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var draw=skin.Renderer;var previous=draw.DrawColor;
            draw.DrawColor=new Color(255,27,40,53);draw.DrawFilledRect(new Rectangle(0,0,Width,Height));
            if(!_failed && Graphics.Renderer is {} renderer && Environment.TickCount64>=_nextFrame)
            {
                _nextFrame=Environment.TickCount64+50; // Bounded to 960x540 at <=20 Hz; no CPU texture readback.
                try
                {
                    if(NavalWorld.CannonScene() is {} scene)
                    {
                        var size=PerspectiveResolution(Width,Height,CrewMode);
                        if(_texture!=null && (_texture.Width!=size.X || _texture.Height!=size.Y)) {_texture.Dispose();_texture=null;}
                        _texture??=renderer.CreateRenderTexture(size.X,size.Y);
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
                DrawCannon(skin);
                var cx=CrewMode && _aim!=default?_aim.X:Width/2;var cy=CrewMode && _aim!=default?_aim.Y:Height/2;
                draw.DrawColor=new Color(210,240,220,165);
                draw.DrawFilledRect(new Rectangle(cx-10,cy,21,1));
                draw.DrawFilledRect(new Rectangle(cx,cy-10,1,21));
            }
            draw.DrawColor=previous;
        }
    }
}
