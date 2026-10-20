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
    private Label? _navalStatus;
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
        var own=Naval.NavalWorld.OwnShip;
        var sailing=own!=null;
        if(sailing && _navalButtons.Count==0)
        {
            for(var i=0;i<Math.Min(10,Items.Count);i++)
            {
                var slot=i;
                var button=new NavalActionButton(this,"NavalSlot"+i) { Font=GameContentManager.Current.GetFont("sourcesansproblack")??Skin.DefaultFont,FontSize=8 };
                button.Clicked+=(_,_)=>Naval.NavalWorld.ActivateSlot(slot);
                _navalButtons.Add(button);
            }
            _navalStatus=new Label(Parent,"NavalHotbarStatus") { AutoSizeToContents=false,
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
            var action=ShipActions.ForSlot(i);var cooldown=Naval.NavalWorld.Cooldown(i);
            var key=KeyCaption(i);
            button.NavalAction=action;button.ReloadMilliseconds=cooldown;
            button.TextAlign=cooldown>0?Pos.Center:Pos.Bottom|Pos.Right;
            button.Text=cooldown>0?Math.Ceiling(cooldown/1000d)+"\n"+key:key;
            button.TooltipText=N(ShipActions.Caption(action));
            button.TextColorOverride=i==8 && own!.Anchored?new Color(255,240,200,70):null;
            button.IsDisabled=own!.Sinking || action==ShipAction.None || Globals.Me?.IsBusy!=false ||
                (action is ShipAction.PortCannons or ShipAction.StarboardCannons && (!own.CannonsEnabled || cooldown>0));
        }
        if(_navalStatus!=null)
        {
            _navalStatus.IsHidden=!sailing;
            if(sailing)
            {
                _navalStatus.SetBounds(X,Y-22,Width,22);
                _navalStatus.Text=string.Format(N("Hull: {0}%   Speed: {1:0.0}"),own!.Health,Math.Abs(own.Speed));
            }
        }
    }
}
