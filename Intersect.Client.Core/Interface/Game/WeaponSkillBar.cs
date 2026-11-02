using System.Text.Json;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

/// <summary>Combat techniques have their own controls; they never occupy spellbook slots.</summary>
internal sealed class WeaponSkillBar : Base
{
    private readonly Label _message;
    private readonly List<(Button Button,MasteryCategory Category,MasteryTalent Talent)> _buttons=[];
    private RoyalCombatConfiguration _config=new();
    private Dictionary<string,long> _experience=[];
    private Dictionary<string,Dictionary<string,int>> _ranks=[];
    private readonly Dictionary<string,long> _cooldowns=[];
    private Guid _weapon;
    private long _pendingUntil;
    internal bool HasMasteryState { get; private set; }

    internal WeaponSkillBar(Canvas canvas):base(canvas,nameof(WeaponSkillBar))
    {
        _message=NavalManagementWindow.Text(this,"WeaponSkillMessage",ClientLanguages.Text("Weapon skills"),0,0,540,22,10);
        Hide();
    }
    internal void Apply(ArcheryStatePacket state)
    {
        if(state.MasteriesReadOnly)return;
        if(state.MasteriesJson.Length>0)
        {
            HasMasteryState=true;
            _config=RoyalCombatConfiguration.FromJson(state.ConfigurationJson);
            _experience=JsonSerializer.Deserialize<Dictionary<string,long>>(state.MasteriesJson)??[];
            _ranks=JsonSerializer.Deserialize<Dictionary<string,Dictionary<string,int>>>(state.MasteryRanksJson)??[];
            Rebuild(EquippedWeapon());
        }
        if(state.WeaponSkillCooldownsJson.Length>0)
        {
            _cooldowns.Clear();
            foreach(var entry in JsonSerializer.Deserialize<Dictionary<string,long>>(state.WeaponSkillCooldownsJson)??[])
                _cooldowns[entry.Key]=Environment.TickCount64+Math.Clamp(entry.Value,0,120000);
            _pendingUntil=0;
            _message.Text=state.Message.Length>0?ClientLanguages.Text(state.Message):ClientLanguages.Text("Weapon skills");
        }
        Update();
    }
    private static Guid EquippedWeapon()
    {
        var player=Globals.Me;var slot=Options.Instance.Equipment.WeaponSlot;
        if(player==null || slot<0 || slot>=player.MyEquipment.Length)return Guid.Empty;
        var index=player.MyEquipment[slot];
        return index>=0 && index<player.Inventory.Count()?player.Inventory[index]?.ItemId??Guid.Empty:Guid.Empty;
    }
    private void Rebuild(Guid weapon)
    {
        _weapon=weapon;
        foreach(var entry in _buttons){entry.Button.Parent=null;entry.Button.Dispose();}_buttons.Clear();
        var category=_config.Masteries.FirstOrDefault(c=>c.WeaponIds.Contains(weapon));
        if(category!=null)
        {
            var ranks=_ranks.GetValueOrDefault(category.Key)??[];
            foreach(var talent in category.Talents.Where(t=>t.Skill.Enabled && ranks.GetValueOrDefault(t.Key)>0 &&
                MasteryRules.Eligible(category,t,_experience.GetValueOrDefault(category.Key),ranks)))
            {
                var button=NavalManagementWindow.Button(this,"WeaponSkill_"+talent.Key,"",0,0,176,32,()=>Use(category,talent));
                button.SetToolTipText(ClientLanguages.Format("Weapon skill · Damage {0}% · Range {1} · Cooldown {2}s",
                    talent.Skill.Power(ranks.GetValueOrDefault(talent.Key)),talent.Skill.Range,talent.Skill.CooldownMilliseconds/1000f));
                _buttons.Add((button,category,talent));
            }
        }
        IsHidden=_buttons.Count==0;
    }
    private void Use(MasteryCategory category,MasteryTalent talent)
    {
        if(Globals.Me is not {} player || player.TargetId==Guid.Empty)
        { _message.Text=ClientLanguages.Text("Select a hostile target first.");return; }
        _pendingUntil=Environment.TickCount64+1000;
        Networking.Network.SendPacket(new ArcheryRequestPacket { Action=ArcheryAction.UseWeaponSkill,
            CategoryKey=category.Key,TalentKey=talent.Key,TargetId=player.TargetId,RequestId=Guid.NewGuid() });
        Update();
    }
    internal void Update()
    {
        var weapon=EquippedWeapon();if(weapon!=_weapon)Rebuild(weapon);
        if(_buttons.Count==0)return;
        var columns=Math.Max(1,Math.Min(3,(Parent?.Width??560)/180));
        SetSize(columns*180,26+((_buttons.Count+columns-1)/columns)*36);
        SetPosition(Math.Max(0,((Parent?.Width??Width)-Width)/2),Math.Max(0,(Parent?.Height??Height)-Height-85));
        _message.Width=Width;
        for(var i=0;i<_buttons.Count;i++)
        {
            var (button,category,talent)=_buttons[i];
            button.SetBounds(i%columns*180,26+i/columns*36,176,32);
            var remaining=Math.Max(0,_cooldowns.GetValueOrDefault(category.Key+"/"+talent.Key)-Environment.TickCount64);
            var name=ClientLanguages.ContentText(ClientLanguages.Text(talent.Name));
            EventPanelText.Fit(button,remaining>0?ClientLanguages.Format("{0} ({1}s)",name,(remaining+999)/1000):name,10);
            button.IsDisabled=remaining>0 || Environment.TickCount64<_pendingUntil;
        }
    }
}
