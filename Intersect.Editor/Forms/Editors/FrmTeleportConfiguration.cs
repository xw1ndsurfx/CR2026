using DarkUI.Forms;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Network.Packets.WorldEvents;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;
internal sealed class FrmTeleportConfiguration:DarkForm
{
    private sealed record Choice(Guid Id,string Name){public override string ToString()=>Name;}
    internal FrmTeleportConfiguration(string json)
    {
        string T(string text)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(text,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        Text=T("Teleport settings");Size=new Size(780,650);MinimumSize=new Size(650,500);StartPosition=FormStartPosition.CenterParent;
        var draft=JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(json)??new();var rules=draft.Teleports;
        var form=new FlowLayoutPanel{Dock=DockStyle.Top,Height=250,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(12)};
        CheckBox Toggle(string text,bool value){var box=new CheckBox{Text=T(text),Checked=value,AutoSize=true};form.Controls.Add(box);return box;}
        var friends=Toggle("Enable friend teleport",rules.FriendsEnabled);var lobby=Toggle("Enable lobby teleport",rules.LobbyEnabled);
        var restrict=Toggle("Restrict lobby travel to allowed maps",rules.RestrictLobbyMaps);var origin=Toggle("The origin map must also be allowed",rules.CheckOriginMap);
        form.Controls.Add(new Label{Text=T("Teleport animation"),AutoSize=true});
        var animation=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=400,Name="TeleportAnimation"};animation.Items.Add(new Choice(Guid.Empty,T("None")));
        foreach(var descriptor in AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>())animation.Items.Add(new Choice(descriptor.Id,descriptor.Name));
        animation.SelectedItem=animation.Items.Cast<Choice>().FirstOrDefault(choice=>choice.Id==rules.AnimationId)??animation.Items[0];form.Controls.Add(animation);
        form.Controls.Add(new Label{Text=T("Teleport cooldown (seconds)"),AutoSize=true});
        var cooldown=new NumericUpDown{Minimum=0,Maximum=3600,Value=rules.CooldownSeconds,Width=140};form.Controls.Add(cooldown);
        var maps=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true,Name="AllowedTeleportMaps"};
        foreach(var map in MapDescriptor.Lookup.Values.OfType<MapDescriptor>().OrderBy(map=>map.Name))maps.Items.Add(new Choice(map.Id,map.Name),rules.AllowedMaps.Contains(map.Id));
        var save=new Button{Text=T("Save"),Dock=DockStyle.Bottom,Height=40};
        save.Click+=(_,_)=>
        {
            rules.FriendsEnabled=friends.Checked;rules.LobbyEnabled=lobby.Checked;rules.RestrictLobbyMaps=restrict.Checked;rules.CheckOriginMap=origin.Checked;
            rules.AnimationId=(animation.SelectedItem as Choice)?.Id??Guid.Empty;rules.CooldownSeconds=(int)cooldown.Value;rules.AllowedMaps=maps.CheckedItems.Cast<Choice>().Select(choice=>choice.Id).ToArray();
            Intersect.Editor.Networking.Network.SendPacket(new SaveWorldActivitiesPacket{Json=JsonConvert.SerializeObject(draft),Section=WorldActivitiesSection.Teleports});Close();
        };
        Controls.Add(maps);Controls.Add(form);Controls.Add(save);MiniGameEditorTheme.Apply(this);
    }
}
