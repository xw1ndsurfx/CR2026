using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Chrome=Intersect.Client.Interface.Game.MarketplaceWindow.MarketChrome;
using Style=Intersect.Client.Interface.Game.MarketplaceWindow.FrameStyle;
namespace Intersect.Client.Interface.Game;
internal class NavalManagementWindow:Window
{
    private readonly ScrollControl _list;private readonly Label _status;private readonly Label _timer;
    private CannonAmmoKind? _browsedAmmo;
    private NavalManagementState _state=new();private long _received;private bool _expiredRefresh;private bool _repairRotated;
    internal static string T(string text)=>NavalSetupText.Get(text,ClientLanguages.Current);
    internal static void Request(NavalManageAction action,CannonAmmoKind ammo=default,int quantity=1,NavalSkillKind skill=default,NavalRepairKind repair=default,Guid target=default,Guid token=default,int value=0,int? expectedUnitPrice=null)=>
        Networking.Network.SendPacket(new NavalManagePacket {Action=action,Ammo=ammo,Quantity=quantity,Skill=skill,Repair=repair,TargetId=target,Token=token,Value=value,ExpectedUnitPrice=expectedUnitPrice});
    internal NavalManagementWindow(Canvas parent,NavalManageAction panel=NavalManageAction.Ammo):base(parent,T(panel==NavalManageAction.Repair?"Ship repairs":panel==NavalManageAction.OfferHelm?"Helm control":panel==NavalManageAction.Skills?"Sailing skills":"Ammunition inventory"),false,"Naval"+panel+"Window")
    {
        SetSize(Math.Min(panel==NavalManageAction.Ammo?460:720,Math.Max(300,parent.Width-24)),Math.Min(panel==NavalManageAction.Ammo?560:590,Math.Max(360,parent.Height-32)));
        Alignment=[Alignments.Center];DeleteOnClose=false;DisableResizing();
        if(panel!=NavalManageAction.Ammo){var chrome=new Chrome(this,"NavalManagementFrame",Style.Window);chrome.SetBounds(12,8,Width-24,Height-35);}
        _status=Text(this,"Status","",24,38,Width-48,42,11);
        _list=new ScrollControl(this,"NavalActivityList") {OverflowX=OverflowBehavior.Hidden,OverflowY=OverflowBehavior.Scroll,AutoHideBars=true};_list.SetBounds(24,85,Width-48,Height-167);
        Shop.ShopWindow.ConfigureNativeScrollbar(_list);_list.AutoHideBars=false;
        _timer=Text(this,"RepairTiming","",24,Height-76,Width-200,32,11);
        Button(this,"CloseNavalActivities","Close",Width-164,Height-72,136,32,()=>ClosePanel());
        Closed+=(_,_)=>{if(_state.RepairToken!=Guid.Empty)Request(NavalManageAction.CancelRepair);};Hide();
    }
    protected override void EnsureInitialized() { }
    internal void Dismiss()=>ClosePanel();
    private void ClosePanel(){if(_state.RepairToken!=Guid.Empty)Request(NavalManageAction.CancelRepair);Hide();}
    internal static Label Text(Base parent,string id,string text,int x,int y,int w,int h,int size=12)
    {
        var label=new Label(parent,id) {Text=text,AutoSizeToContents=false,Font=GameContentManager.Current.GetFont("sourcesanspro")??parent.Skin.DefaultFont,
            FontSize=size,TextColorOverride=new Color(255,241,222,185),TextAlign=Pos.Left|Pos.CenterV,MouseInputEnabled=false};label.SetBounds(x,y,w,h);return label;
    }
    internal static Button Button(Base parent,string id,string caption,int x,int y,int w,int h,Action action)
    {
        var b=new Button(parent,id){Text=T(caption),Font=GameContentManager.Current.GetFont("sourcesanspro")??parent.Skin.DefaultFont,FontSize=11};b.SetBounds(x,y,w,h);
        StyleButton(b);b.Clicked+=(_,_)=>action();return b;
    }

    internal static void StyleButton(Button button)
    {
        button.SetStateTexture(ComponentState.Normal,"control_button.png");
        button.SetStateTexture(ComponentState.Hovered,"control_button_hovered.png");
        button.SetStateTexture(ComponentState.Active,"control_button_clicked.png");
    }
    internal void Apply(NavalManagementState state)
    {
        if(state.Mode==NavalManageAction.CancelRepair){Hide();return;}
        if(_state.RepairToken!=state.RepairToken)_repairRotated=false;
        if(state.Mode==NavalManageAction.Ammo)Naval.NavalWorld.SelectedAmmo=state.SelectedAmmo;
        _state=state;_received=Environment.TickCount64;_expiredRefresh=false;_list.DeleteAllChildren();
        _status.Text=(string.IsNullOrEmpty(state.Message)?"":T(state.Message)+"\n")+(state.Mode==NavalManageAction.Ammo?string.Format(T("Ammunition: {0}/{1} - In use: {2}"),state.AmmoUsed,state.AmmoCapacity,T(CannonAmmunition.Caption(state.SelectedAmmo))):string.Format(T("Sailing {0} - Gunnery {1} - Shipwright {2}"),state.SailingLevel,state.GunneryLevel,state.Shipwright.Level));
        var width=_list.Width-24;var y=4;
        if(state.Mode==NavalManageAction.Repair)
        {
            void Progress(string id,string name,SailingProgress progress)
            {
                Text(_list,id+"Name",string.Format(T("{0} - Shipwright level {1}"),name,progress.Level),8,y,width-16,25,12);y+=27;
                var bar=new PlayerProfileWindow.ProfileProgressBar(_list,id+"Experience"){Progress=progress.Required<=0?1:(float)progress.Experience/progress.Required};bar.SetBounds(8,y,width-16,18);
                Text(bar,id+"ExperienceText",progress.Required<=0?T("Maximum level"):string.Format(T("{0}/{1} EXP"),progress.Experience,progress.Required),4,0,bar.Width-8,18,10);y+=26;
            }
            Progress("OwnShipwright",Globals.Me?.Name??T("You"),state.Shipwright);
            Text(_list,"RepairCrewCaption",T("Shipwrights at work"),8,y,width-16,25,12);y+=28;
            foreach(var member in state.RepairParticipants.Where(m=>m.PlayerId!=Globals.Me?.Id))Progress("Shipwright"+member.PlayerId,member.Name,member.Progress);

            if(state.RepairToken==Guid.Empty)
            {
                Text(_list,"RepairChoice",T("Choose the wooden pieces for this repair puzzle."),8,y,width-16,44,12);y+=54;
                foreach(var kind in Enum.GetValues<NavalRepairKind>())
                {var k=kind;Button(_list,"Repair"+k,NavalRepairPuzzle.Caption(k),8,y,width-16,48,()=>Request(NavalManageAction.Repair,repair:k));y+=60;}
            }
            else
            {
                Text(_list,"RepairInstructions",T("Fit the wooden pieces into the hull breaches. Rotate a piece before placing it; restart if it no longer fits."),8,y,width-16,54,12);y+=58;
                Text(_list,"RepairHits",T(NavalRepairPuzzle.Caption(state.RepairKind))+" - "+string.Format(T("Repairs: {0}/{1}"),state.RepairHits,state.RepairRequiredHits),8,y,width-16,28,14);y+=32;
                Button(_list,"RotateRepairPiece","Rotate piece",8,y,(width-28)/2,32,()=>_repairRotated=!_repairRotated);
                Button(_list,"RestartRepairPuzzle","Restart puzzle",16+(width-28)/2,y,(width-28)/2,32,()=>Request(NavalManageAction.Repair,repair:state.RepairKind));y+=40;
                var board=new RepairPuzzleBoard(_list,state,()=>_repairRotated);board.SetBounds(8,y,width-16,Math.Max(1,state.RepairCells.Length/6)*36);y=board.Bottom+8;
            }
        }
        else if(state.Mode is NavalManageAction.Skills or NavalManageAction.UseSkill or NavalManageAction.BuySkill)
        {
            foreach(var entry in state.Skills.Where(s=>s.Owned))
            {
                var d=entry.Definition;
                Text(_list,"SkillName"+d.Kind,T(NavalSkillDefinition.Caption(d.Kind)),64,y,width-72,27,14);
                if(!string.IsNullOrEmpty(d.Icon)){var icon=new ImagePanel(_list,"SkillIcon"+d.Kind){MaintainAspectRatio=true,ShouldDrawBackground=false,MouseInputEnabled=false};icon.SetBounds(8,y+4,42,42);icon.Texture=GameContentManager.Current.GetTexture(Intersect.Client.Framework.Content.TextureType.Spell,d.Icon);}
                Text(_list,"SkillDetails"+d.Kind,string.Format(T("Level {0} - Duration {1}s - Recharge {2}s"),d.RequiredLevel,d.DurationSeconds,d.CooldownSeconds),8,y+29,width-16,26,11);
                var button=Button(_list,"SkillAction"+d.Kind,entry.Owned?"Activate":"Learn skill",8,y+62,width-16,32,()=>Request(entry.Owned?NavalManageAction.UseSkill:NavalManageAction.BuySkill,skill:d.Kind));
                button.IsDisabled=state.SailingLevel<d.RequiredLevel || !entry.Owned && !state.SkillShop || entry.Owned && (entry.CooldownMilliseconds>0 || state.HelmsmanId!=Globals.Me?.Id);
                y+=112;
            }
        }
        else if(state.Mode is not (NavalManageAction.OfferHelm or NavalManageAction.AcceptHelm or NavalManageAction.ReclaimHelm))
        {
            var columns=Math.Max(2,(width-16)/78);var index=0;
            foreach(var ammo in state.Ammo)
            {
                var a=ammo;var x=8+index%columns*78;var row=index/columns;index++;
                var slot=new Button(_list,"AmmoSlot"+a.Kind) {Text="",TooltipText=T(CannonAmmunition.Caption(a.Kind))};slot.SetBounds(x,8+row*80,70,72);
                var icon=new ImagePanel(slot,"AmmoIcon") {MaintainAspectRatio=true,ShouldDrawBackground=false,MouseInputEnabled=false};icon.SetBounds(16,18,36,34);
                var item=Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(a.ItemId);
                icon.Texture=!string.IsNullOrEmpty(a.Icon)?GameContentManager.Current.GetTexture(Intersect.Client.Framework.Content.TextureType.Spell,a.Icon):
                    item==null?null:GameContentManager.Current.GetTexture(item.UsesSpellIcon?Intersect.Client.Framework.Content.TextureType.Spell:Intersect.Client.Framework.Content.TextureType.Item,item.DisplayIcon);
                if(a.Kind==state.SelectedAmmo)
                {
                    var active=Text(slot,"AmmoInUse",T("Active"),0,0,70,18,10);
                    active.TextAlign=Pos.Center;active.ShouldDrawBackground=true;active.RenderColor=new Color(255,45,99,61);
                    slot.TooltipText+=" - "+T("In use");
                }
                Text(slot,"AmmoQuantity",a.Quantity<0?T("Free"):a.Quantity.ToString(),4,52,62,18,10);
                slot.Clicked+=(_,_)=>{_browsedAmmo=a.Kind;Apply(state);};
            }
            y=20+((index+columns-1)/columns)*80;
            var selected=state.Ammo.FirstOrDefault(a=>a.Kind==(_browsedAmmo??state.SelectedAmmo))??state.Ammo.FirstOrDefault();
            if(selected!=null)
            {
                Text(_list,"AmmoSelected",T(CannonAmmunition.Caption(selected.Kind)),8,y,width-16,28,14);y+=34;
                Text(_list,"AmmoRequirements",string.Format(T("Ammo: {0} - Inventory: {1} - Sailing {2} / Gunnery {3}"),selected.Quantity<0?T("Free"):selected.Quantity.ToString(),selected.MainQuantity,selected.SailingLevel,selected.GunneryLevel),8,y,width-16,42,11);y+=50;
                var inUse=selected.Kind==state.SelectedAmmo;
                var use=Button(_list,"AmmoUse",inUse?"In use":"Use",8,y,(width-28)/2,34,()=>Request(NavalManageAction.SelectAmmo,selected.Kind));use.IsDisabled=inUse || !selected.Available || selected.Quantity==0 && selected.MainQuantity==0;
                var withdraw=Button(_list,"AmmoWithdraw","Withdraw",16+(width-28)/2,y,(width-28)/2,34,()=>Request(NavalManageAction.Withdraw,selected.Kind,Math.Min(10,selected.Quantity)));withdraw.IsDisabled=selected.Quantity<=0;
            }
        }

        if(state.ShipId!=Guid.Empty && state.Mode is NavalManageAction.OfferHelm or NavalManageAction.AcceptHelm or NavalManageAction.ReclaimHelm)
        {
            y+=8;
            if(state.HelmOfferId!=Guid.Empty){Button(_list,"AcceptHelm","Accept helm",8,y,width-16,34,()=>Request(NavalManageAction.AcceptHelm,token:state.HelmOfferId));y+=44;}
            if(state.OwnerId==Globals.Me?.Id)
            {
                Button(_list,"ReclaimHelm","Reclaim helm",8,y,width-16,34,()=>Request(NavalManageAction.ReclaimHelm));y+=44;
                foreach(var member in (Naval.NavalWorld.OwnShip?.CrewIds??[]))
                {
                    var id=member;var name=Naval.NavalWorld.CrewState.Members.FirstOrDefault(m=>m.PlayerId==id)?.Name??id.ToString();
                    var button=Button(_list,"OfferHelm"+id,"Offer helm",8,y,width-16,34,()=>Request(NavalManageAction.OfferHelm,target:id));button.TooltipText=name;y+=44;
                }
            }
        }
        _list.SetInnerSize(width,Math.Max(_list.Height,_list.Children.Select(child=>child.Bottom+8).DefaultIfEmpty(0).Max()));_list.UpdateScrollBars();Show();BringToFront();
    }
    internal void Tick()
    {
        if(IsHidden)return;
        if(_state.Mode==NavalManageAction.Skills)
        {
            foreach(var entry in _state.Skills.Where(s=>s.Owned))
            {
                var button=_list.Children.OfType<Button>().FirstOrDefault(b=>b.Name=="SkillAction"+entry.Definition.Kind);
                if(button==null)continue;var remaining=Math.Max(0,entry.CooldownMilliseconds-(Environment.TickCount64-_received));
                button.IsDisabled=remaining>0 || _state.HelmsmanId!=Globals.Me?.Id || _state.SailingLevel<entry.Definition.RequiredLevel;
                button.TooltipText=remaining>0?string.Format(T("Reload: {0}s"),(remaining+999)/1000):T(NavalSkillDefinition.Caption(entry.Definition.Kind));
            }
        }
        if(_state.Mode==NavalManageAction.Repair && !_expiredRefresh && Environment.TickCount64-_received>=1000){_expiredRefresh=true;Request(NavalManageAction.RepairMenu);}
        if(_state.RepairToken==Guid.Empty)return;
        _timer.Text=string.Format(T("Time: {0}s"),Math.Max(0,(_state.RepairRemainingMilliseconds-(Environment.TickCount64-_received))/1000));
    }

}

internal sealed class NavalDockWindow(Intersect.Client.Framework.Gwen.Control.Canvas parent):Window(parent,NavalManagementWindow.T("Dock nearby"),false,"NavalDockPrompt")
{protected override void EnsureInitialized() { }}
