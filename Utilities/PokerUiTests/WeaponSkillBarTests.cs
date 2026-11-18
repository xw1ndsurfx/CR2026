using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Items;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Server;
using Base=Intersect.Client.Framework.Gwen.Control.Base;

internal static class WeaponSkillBarTests
{
    [ModuleInitializer] internal static void Run()
    {
        var oldMe=Globals.Me;
        try
        {
            _=new TestContentManager();using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
            skin.Colors.Button.Normal=skin.Colors.Button.Hover=skin.Colors.Button.Active=skin.Colors.Button.Disabled=Color.White;
            using var canvas=new Canvas(skin) { Size=new Point(800,600) };
            var overlay=new WindowControl(canvas,"Rewards"); overlay.Hide();
            const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
            var player=(Intersect.Client.Entities.Player)RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Client.Entities.Player));GC.SuppressFinalize(player);
            player.MyEquipment=Enumerable.Repeat(-1,Options.Instance.Equipment.Slots.Count).ToArray();
            var weapon=Guid.NewGuid();
            typeof(Intersect.Client.Entities.Entity).GetField("<Inventory>k__BackingField",hidden)!.SetValue(player,new IItem[]{new Intersect.Client.Items.Item { ItemId=weapon,Quantity=1 }});
            player.MyEquipment[Options.Instance.Equipment.WeaponSlot]=0;Globals.Me=player;
            var config=new RoyalCombatConfiguration();var category=config.Masteries.Single(c=>c.Key=="swords");category.WeaponIds=[weapon];
            var talent=category.Talents.First(t=>t.Skill.Enabled);
            talent.Skill.Icon="quick-stabs.png";
            var type=typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.WeaponSkillBar",true)!;
            var canShow=true;
            var bar=(Base)Activator.CreateInstance(type,hidden,null,[canvas,(Func<bool>)(()=>canShow),null],null)!;
            void Apply(ArcheryStatePacket packet)=>type.GetMethod("Apply",hidden)!.Invoke(bar,[packet]);
            void Update()=>type.GetMethod("Update",hidden)!.Invoke(bar,null);
            Apply(new ArcheryStatePacket { ConfigurationJson=config.ToJson(),MasteriesJson=JsonSerializer.Serialize(new Dictionary<string,long>{{category.Key,500}}),
                MasteryRanksJson=JsonSerializer.Serialize(new Dictionary<string,Dictionary<string,int>>{{category.Key,new(){{talent.Key,1}}}}) });
            var button=bar.Children.OfType<Button>().First();
            Check(!bar.IsHidden && !button.IsDisabled,"Learned active skill is available in its own bar");
            Check(button.Width==34 && button.Height==34 && button.TooltipText.Contains(talent.Name),"Compact icon slots retain the skill name in their tooltip");
            canShow=false;Update();Check(bar.IsHidden,"The current game mode can hide weapon skills");
            canShow=true;Update();Check(!bar.IsHidden,"Returning to land restores weapon skills");
            Apply(new ArcheryStatePacket { WeaponSkillCooldownsJson=JsonSerializer.Serialize(new Dictionary<string,long>{{category.Key+"/"+talent.Key,5000}}) });
            Check(button.IsDisabled && int.TryParse(button.Text,out var seconds) && seconds>0,"Server cooldown is visible and prevents another activation");
            Apply(new ArcheryStatePacket { MasteriesReadOnly=true,MasteriesJson="{}",ConfigurationJson=config.ToJson() });
            Check(bar.Children.OfType<Button>().Count()==10,"Inspecting another player does not overwrite combat skills");
            player.MyEquipment[Options.Instance.Equipment.WeaponSlot]=-1;Update();Check(bar.IsHidden,"Unequipping the weapon hides its skills");
            player.MyEquipment[Options.Instance.Equipment.WeaponSlot]=0;Update();
            Check(!bar.IsHidden && bar.Children.OfType<Button>().First().IsDisabled,"Re-equipping does not reset cooldowns");
            Apply(new ArcheryStatePacket { WeaponSkillCooldownsJson="{}" });
            Check(!bar.Children.OfType<Button>().First().IsDisabled,"An authoritative cooldown refresh restores activation");
            var slots = Enumerable.Repeat("", WeaponSkillLoadout.SlotCount).ToArray(); slots[7] = talent.Key;
            ArcheryStatePacket LayoutPacket() => new() { ConfigurationJson = config.ToJson(),
                MasteriesJson = JsonSerializer.Serialize(new Dictionary<string,long>{{category.Key,500}}),
                MasteryRanksJson = JsonSerializer.Serialize(new Dictionary<string,Dictionary<string,int>>{{category.Key,new(){{talent.Key,1}}}}),
                WeaponSkillSlotsJson = JsonSerializer.Serialize(new Dictionary<string,string[]>{{category.Key,slots}}) };
            Apply(LayoutPacket());
            var arranged = bar.Children.OfType<Button>().ToArray();
            Check(arranged.Length == 10 && arranged[0].IsDisabled && !arranged[7].IsDisabled && arranged[7].TooltipText.Contains(talent.Name),
                "Server slot selection moves a learned skill into the chosen slot");
            slots[7] = ""; Apply(LayoutPacket());
            Check(bar.Children.OfType<Button>().All(b => b.IsDisabled), "Explicit empty slots do not silently refill with automatic skills");
            var visibility=typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.WeaponSkillHudVisibility",true)!;
            bool WindowOpen()=>(bool)visibility.GetMethod("HasOpenWindow",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[canvas])!;
            overlay.Show();
            Check(WindowOpen(),"A reward window suppresses the combat bar even if created before the bar");
            overlay.Hide();Check(!WindowOpen(),"Closing rewards makes the combat bar available again");
            System.Console.WriteLine("PASS separate weapon skill bar: learned ranks, equipped category, authoritative cooldown, inspection isolation and weapon switching");
        }
        finally { Globals.Me=oldMe; }
    }
    private static void Check(bool value,string text) { if(!value)throw new Exception(text); }
}
