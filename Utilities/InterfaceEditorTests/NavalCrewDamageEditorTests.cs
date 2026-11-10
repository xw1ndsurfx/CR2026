using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;

internal static class NavalCrewDamageEditorTests
{
    internal static void Run()
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        var npc=new NPCDescriptor(Guid.NewGuid()){Name="Chosen captain",Sprite="captain.png"};
        var animation=new AnimationDescriptor(Guid.NewGuid()){Name="Small deck flames"};
        NPCDescriptor.Lookup.Set(npc.Id,npc);AnimationDescriptor.Lookup.Set(animation.Id,animation);
        try
        {
            using var editor=new FrmNavalConfiguration(new NavalConfigurationPacket {NavigationJson="{}",CatalogueJson="{}",NavigationVersion="1",CatalogueVersion="1"});
            Check(editor.Controls.Find("NavalShipPresetCreator",true).Length==3,"Test ships, ship shops and NPC patrols can create a matching-state ship preset");
            var fields=typeof(FrmNavalConfiguration).GetMethod("Fields",hidden)!;
            var patrol=new HerboniaPatrol();
            using var patrolPanel=(TableLayoutPanel)fields.Invoke(editor,[patrol,0,new[]{"Ship","Loot"}])!;
            foreach(var name in new[]{"DeckCaptainNpcId","PortGunner1NpcId","PortGunner2NpcId","PortGunner3NpcId","StarboardGunner1NpcId","StarboardGunner2NpcId","StarboardGunner3NpcId","PortGunner4NpcId","PortGunner5NpcId","PortGunner6NpcId","StarboardGunner4NpcId","StarboardGunner5NpcId","StarboardGunner6NpcId"})
            {
                var choices=(ComboBox)patrolPanel.Controls[name]!;
                choices.SelectedItem=choices.Items.Cast<object>().Single(c=>((Guid)c.GetType().GetProperty("Id")!.GetValue(c)!)==npc.Id);
                Check((Guid)typeof(HerboniaPatrol).GetProperty(name)!.GetValue(patrol)! == npc.Id,"Every station can choose an existing NPC");
            }
            var boarding=(CheckBox)patrolPanel.Controls["CanBoardPlayerShips"]!;Check(!boarding.Checked,"Enemy boarding is opt-in");boarding.Checked=true;Check(patrol.CanBoardPlayerShips,"Checkbox changes patrol configuration");
            var zone=new ShipDamageZone();
            using var zonePanel=(TableLayoutPanel)fields.Invoke(editor,[zone,0,new[]{"Id"}])!;
            var animations=(ComboBox)zonePanel.Controls["DamageAnimationId"]!;
            animations.SelectedItem=animations.Items.Cast<object>().Single(c=>((Guid)c.GetType().GetProperty("Id")!.GetValue(c)!)==animation.Id);
            ((NumericUpDown)zonePanel.Controls["X"]!).Value=-.5m;((NumericUpDown)zonePanel.Controls["Y"]!).Value=1.4m;
            Check(zone.DamageAnimationId==animation.Id && zone.X==-.5 && zone.Y==1.4 && zone.IsValid(),"Damage zones use real animations and allow port-side coordinates");
            var model=new ShipDefinition {FishingZoneId=Guid.NewGuid()};
            using var modelPanel=(TableLayoutPanel)fields.Invoke(editor,[model,0,new[]{"AdditionalData"}])!;
            Check(modelPanel.Controls["DamageZones"] is Button,"Damage zones open a dedicated editor rather than editing an array as text");
            var activity=(ComboBox)modelPanel.Controls["DeckActivity"]!;activity.SelectedIndex=(int)ShipDeckActivity.Fishing;
            Check(model.DeckActivity==ShipDeckActivity.Fishing,"A ship can replace cannon stations with fishing stations");
            var zoneChoices=modelPanel.Controls["FishingZoneId"]!.Controls.OfType<ComboBox>().Single();
            Check((Guid)zoneChoices.SelectedItem!.GetType().GetProperty("Id")!.GetValue(zoneChoices.SelectedItem)! == model.FishingZoneId,"A saved fishing zone is preserved before its editor resources arrive");
            var monster=new NavalMonsterDefinition();
            using var monsterPanel=(TableLayoutPanel)fields.Invoke(editor,[monster,0,new[]{"Loot"}])!;
            var monsterNpc=(ComboBox)monsterPanel.Controls["TentacleNpcId"]!;monsterNpc.SelectedItem=monsterNpc.Items.Cast<object>().Single(c=>((Guid)c.GetType().GetProperty("Id")!.GetValue(c)!)==npc.Id);
            var boss=(ComboBox)monsterPanel.Controls["BossAnimationId"]!;boss.SelectedItem=boss.Items.Cast<object>().Single(c=>((Guid)c.GetType().GetProperty("Id")!.GetValue(c)!)==animation.Id);
            ((NumericUpDown)monsterPanel.Controls["BossAnimationOffsetY"]!).Value=-120;
            ((ComboBox)monsterPanel.Controls["Kind"]!).SelectedIndex=(int)NavalMonsterKind.Leviathan;
            Check(monster.TentacleNpcId==npc.Id && monster.BossAnimationId==animation.Id && monster.BossAnimationOffsetY==-120 && monster.Kind==NavalMonsterKind.Leviathan,"Naval Monsters chooses real NPCs, boss animations, negative offsets and monster kinds");
            Console.WriteLine("PASS naval crew and damage editor: thirteen NPC selectors and Naval Monsters, opt-in enemy boarding, existing animations, negative port coordinates and dedicated damage-zone editor");
        }
        finally{NPCDescriptor.Lookup.DeleteAt(npc.Id);AnimationDescriptor.Lookup.DeleteAt(animation.Id);}
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
