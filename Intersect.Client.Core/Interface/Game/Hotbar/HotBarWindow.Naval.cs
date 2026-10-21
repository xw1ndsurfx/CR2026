using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Core.Controls;
using Intersect.Client.Framework.Input;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Utilities;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Naval;

namespace Intersect.Client.Interface.Game.Hotbar;

public partial class HotBarWindow
{
    private readonly List<NavalActionButton> _navalButtons=[];
    private NavalSailingBar? _navalStatus;
    private NavalSailingBar? _sailingBar;
    private NavalTargetHud? _targetHud;
    private NavalCargoWindow? _cargoWindow;
    private NavalCannonViewWindow? _cannonWindow;
    private NavalRewardHud? _rewardHud;
    private NavalActiveSkillHud? _activeSkillHud;
    private bool _wasSailing;
    private bool[] _hiddenBeforeSailing=[];
    private static string KeyCaption(int slot)
    {
        if(Controls.ActiveControls?.TryGetMappingFor(Control.HotkeyOffset+slot+1,out var mapping)!=true) return "-";
        return NavalKeyCaption(mapping.Bindings.FirstOrDefault());
    }
    internal static string NavalKeyCaption(ControlBinding? binding)
    {
        if(binding==null || binding.Key==Keys.None) return "-";
        static string KeyText(Keys key,bool modifier)
        {
            var id=key.GetKeyId(isModifier:modifier).ToLowerInvariant();
            return Strings.Keys.KeyDictionary.TryGetValue(id,out var text)?text.ToString():id;
        }
        var key=KeyText(binding.Key,false);
        return binding.Modifier==Keys.None?key:Strings.Keys.KeyNameWithModifier.ToString(KeyText(binding.Modifier,true),key);
    }
    private string N(string text)=>NavalSetupText.Get(text,ClientLanguages.Current);
    private void UpdateNavalBar()
    {
        Interface.InGameUiOrNull?.UpdateNavalCrew();
        var reward=Naval.NavalWorld.RewardFeed.Current(Environment.TickCount64);
        if(reward!=null && _rewardHud==null) _rewardHud=new NavalRewardHud(Parent);
        _rewardHud?.Update(reward,Y-64,Interface.InGameUiOrNull?.SuppressStatusHuds==true);
        var own=Naval.NavalWorld.OwnShip;
        var sailing=own!=null;
        if (sailing && _activeSkillHud==null) _activeSkillHud=new NavalActiveSkillHud(Parent);
        _activeSkillHud?.Update(own,Interface.InGameUiOrNull?.SuppressStatusHuds==true);
        if(sailing && _cannonWindow==null) _cannonWindow=new NavalCannonViewWindow(Parent);
        _cannonWindow?.Update(own,Naval.NavalWorld.CannonViewOpen);
        if((sailing || Naval.NavalWorld.ShoreCargo!=null) && _cargoWindow==null) _cargoWindow=new NavalCargoWindow(Parent);
        _cargoWindow?.Update(own??Naval.NavalWorld.ShoreCargo,Naval.NavalWorld.CargoOpen);
        if(sailing && _targetHud==null) _targetHud=new NavalTargetHud(Parent);
        _targetHud?.Update(sailing && Interface.InGameUiOrNull?.SuppressStatusHuds!=true ? Naval.NavalWorld.CombatTarget : null);
        if(sailing && _navalButtons.Count==0)
        {
            for(var i=0;i<Math.Min(10,Items.Count);i++)
            {
                var slot=i;
                var button=new NavalActionButton(this,"NavalSlot"+i) { Font=GameContentManager.Current.GetFont("sourcesansproblack")??Skin.DefaultFont,FontSize=8 };
                button.Clicked+=(_,_)=>Naval.NavalWorld.ActivateSlot(slot);
                _navalButtons.Add(button);
            }
            _sailingBar=new NavalSailingBar(Parent) {AutoSizeToContents=false,Font=GameContentManager.Current.GetFont("sourcesansproblack")??Skin.DefaultFont,
                FontSize=10,TextAlign=Pos.Center,MouseInputEnabled=false};
            _navalStatus=new NavalSailingBar(Parent,"NavalHullHealth") { Hull=true,AutoSizeToContents=false,
                Font=GameContentManager.Current.GetFont("sourcesanspro")??Skin.DefaultFont,FontSize=11,
                TextAlign=Pos.Center,MouseInputEnabled=false };
        }
        if(sailing!=_wasSailing)
        {
            if(sailing) { _hiddenBeforeSailing=Items.Select(item=>item.IsHidden).ToArray(); foreach(var item in Items) item.Hide(); }
            else for(var i=0;i<Items.Count && i<_hiddenBeforeSailing.Length;i++) Items[i].IsHidden=_hiddenBeforeSailing[i];
            _wasSailing=sailing;
        }
        for(var i=0;i<_navalButtons.Count;i++)
        {
            var button=_navalButtons[i];button.IsHidden=!sailing;
            if(!sailing) continue;
            var item=Items[i];button.SetBounds(item.X,item.Y,item.Width,item.Height);
            if(Naval.NavalWorld.GunneryBarActive)
            {
                var ammo=Naval.NavalWorld.SlotAmmunition(i);
                var selected=ammo!=null && ammo.Kind==Naval.NavalWorld.SelectedAmmo;
                button.Ammunition=ammo?.Kind;button.IconFilename=ammo?.Icon??"";button.NavalAction=ShipAction.None;
                button.ReloadMilliseconds=0;
                button.TextColorOverride=selected?new Color(255,240,200,70):null;
                button.TooltipText=ammo==null?"":string.Format(N("{0} · Level {1} · {2}"),N(CannonAmmunition.Caption(ammo.Kind)),ammo.RequiredLevel,
                    ammo.Quantity<0?N("Free"):string.Format(N("{0} shots"),ammo.Quantity));
                button.IsDisabled=own!.Sinking || ammo?.Available!=true || Globals.Me?.IsBusy!=false;
                button.SelectedAmmunition=selected;
                button.SetCaption(KeyCaption(i),ammo==null?null:NavalActionButton.AmmoQuantityCaption(ammo.Quantity));
                continue;
            }
            button.Ammunition=null;button.SelectedAmmunition=false;
            var action=ShipActions.ForSlot(i);var cooldown=Naval.NavalWorld.Cooldown(i);
            var key=KeyCaption(i);
            button.IconFilename=Naval.NavalWorld.Presentation.Icon(action);
            button.NavalAction=action;button.ReloadMilliseconds=cooldown;
            button.TooltipText=N(i==3?"Sailing skills":i==4?"Ammo":i==5?"Helm control":ShipActions.Caption(action));
            button.TextColorOverride=i==8 && own!.Anchored?new Color(255,240,200,70):null;
            button.IsDisabled=own!.Sinking || (action==ShipAction.None && !(i is >=3 and <=5 && Naval.NavalWorld.Driver(own)==Globals.Me?.Id)) ||
                (Naval.NavalWorld.Driver(own)!=Globals.Me?.Id && action is not (ShipAction.CannonView or ShipAction.Crew or ShipAction.Cargo or ShipAction.LeaveHelm)) || Globals.Me?.IsBusy!=false ||
                (action is ShipAction.PortCannons or ShipAction.StarboardCannons && (!own.CannonsEnabled || cooldown>0));
            button.SetCaption(key,cooldown>0?Math.Ceiling(cooldown/1000d).ToString(System.Globalization.CultureInfo.InvariantCulture):null,true);
        }
        if(_sailingBar!=null)
        {
            _sailingBar.IsHidden=!sailing;
            if(sailing)
            {
                _sailingBar.SetBounds(X,Y-27,Width,23);
                var progress=Naval.NavalWorld.RoleProgress;var required=progress.Required;var xp=progress.Experience;
                _sailingBar.Value=required<=0?1:(float)Math.Clamp(xp/(double)required,0,1);
                _sailingBar.Text=required<=0?string.Format(N(progress.Gunnery?"Gunnery · Level {0} · MAX":"Sailing · Level {0} · MAX"),progress.Level):
                    string.Format(N(progress.Gunnery?"Gunnery · Level {0} · {1}/{2} EXP":"Sailing · Level {0} · {1}/{2} EXP"),progress.Level,xp,required);
            }
        }
        if(_navalStatus!=null)
        {
            _navalStatus.IsHidden=!sailing;
            if(sailing)
            {
                _navalStatus.SetBounds(X,Y-54,Width,23);
                _navalStatus.Value=own!.Health/(float)Math.Max(1,own.MaximumHealth);
                _navalStatus.Text=string.Format(N("Hull: {0}/{1} · Cargo: {2}/{3} · Speed: {4:0.0} · Crew: {5}/{6}"),own!.Health,own.MaximumHealth,own.Cargo.Length,own.CargoCapacity,Math.Abs(own.Speed),Naval.NavalWorld.CrewState.Members.Length,Naval.NavalWorld.CrewState.Capacity);
                if(own!.CargoCapacity>0 && own.Cargo.Length>=own.CargoCapacity)_navalStatus.Text+=" - MAX";
                EventPanelText.Fit(_navalStatus,_navalStatus.Text,11,8);
            }
        }
    }
}
