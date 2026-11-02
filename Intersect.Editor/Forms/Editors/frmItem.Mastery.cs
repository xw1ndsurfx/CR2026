using DarkUI.Controls;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;

namespace Intersect.Editor.Forms.Editors;

public partial class FrmItem
{
    private readonly DarkGroupBox _masteryRequirementsGroup = new() { Name="ItemMasteryRequirements", Width=610, Height=140 };
    private readonly DarkComboBox _itemMastery = new() { Name="ItemMasteryCategory", Dock=DockStyle.Fill, DropDownStyle=ComboBoxStyle.DropDownList };
    private readonly DarkNumericUpDown _itemMasteryLevel = new() { Name="ItemMasteryLevel", Dock=DockStyle.Fill, Minimum=1, Maximum=100 };
    private bool _refreshingItemMastery;

    private void InitializeItemMasteryControls()
    {
        _masteryRequirementsGroup.Text=AutoText("Weapon mastery requirement");
        var table=new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(12,8,12,8), ColumnCount=2, RowCount=3 };
        table.ColumnStyles.Add(new(SizeType.AutoSize)); table.ColumnStyles.Add(new(SizeType.Percent,100));
        table.Controls.Add(new Label { Text=AutoText("Weapon mastery requirement"), AutoSize=true, Margin=new Padding(3,6,16,3) },0,0);
        table.Controls.Add(_itemMastery,1,0);
        table.Controls.Add(new Label { Text=AutoText("Required mastery level"), AutoSize=true, Margin=new Padding(3,6,16,3) },0,1);
        table.Controls.Add(_itemMasteryLevel,1,1);
        var hint=new Label { Text=AutoText("The mastery requirement applies to every usage requirement alternative."), AutoSize=true, Dock=DockStyle.Fill, MaximumSize=new Size(560,0) };
        table.Controls.Add(hint,0,2); table.SetColumnSpan(hint,2);
        _masteryRequirementsGroup.Controls.Add(table);
        _itemMastery.SelectedIndexChanged+=(_,_)=>SaveItemMasteryRequirement();
        _itemMasteryLevel.ValueChanged+=(_,_)=>SaveItemMasteryRequirement();
        RoyalCombatEditorState.Changed+=RefreshItemMasteryRequirement;
        Disposed+=(_,_)=>RoyalCombatEditorState.Changed-=RefreshItemMasteryRequirement;
        Shown+=(_,_)=> { if(Networking.Network.Connected) Networking.Network.SendPacket(new ArcheryRequestPacket { Action=ArcheryAction.GetConfiguration }); };
        RefreshItemMasteryRequirement();
    }

    private void RefreshItemMasteryRequirement()
    {
        _refreshingItemMastery=true;
        try
        {
            var condition=mEditorItem?.UsageRequirements?.Lists.SelectMany(l=>l.Conditions).OfType<WeaponMasteryLevelCondition>().FirstOrDefault();
            _itemMastery.Items.Clear(); _itemMastery.Items.Add(new MasteryChoice(null));
            foreach(var category in RoyalCombatEditorState.Current?.Masteries??[]) _itemMastery.Items.Add(new MasteryChoice(category));
            if(condition!=null && !_itemMastery.Items.Cast<MasteryChoice>().Any(c=>c.Category?.Key==condition.MasteryKey))
                _itemMastery.Items.Add(new MasteryChoice(new MasteryCategory { Key=condition.MasteryKey, Name=condition.MasteryName, Cap=Math.Clamp(condition.Value,2,100) }));
            _itemMastery.SelectedItem=_itemMastery.Items.Cast<MasteryChoice>().FirstOrDefault(c=>c.Category?.Key==condition?.MasteryKey)??_itemMastery.Items[0];
            _itemMasteryLevel.Maximum=(_itemMastery.SelectedItem as MasteryChoice)?.Category?.Cap??100;
            _itemMasteryLevel.Value=Math.Clamp(condition?.Value??1,1,(int)_itemMasteryLevel.Maximum);
            _itemMastery.Enabled=mEditorItem!=null;
            _itemMasteryLevel.Enabled=mEditorItem!=null && (_itemMastery.SelectedItem as MasteryChoice)?.Category!=null;
        }
        finally { _refreshingItemMastery=false; }
    }

    private void SaveItemMasteryRequirement()
    {
        if(_refreshingItemMastery || mEditorItem==null) return;
        var category=(_itemMastery.SelectedItem as MasteryChoice)?.Category;
        _refreshingItemMastery=true;
        try
        {
            _itemMasteryLevel.Maximum=category?.Cap??100;
            _itemMasteryLevel.Enabled=category!=null;
            mEditorItem.UsageRequirements??=new();
            SetItemMasteryRequirement(mEditorItem.UsageRequirements,category,(int)_itemMasteryLevel.Value);
        }
        finally { _refreshingItemMastery=false; }
    }

    internal static void SetItemMasteryRequirement(ConditionLists requirements, MasteryCategory? category, int level)
    {
        if(requirements.Lists.Count==0 && category!=null) requirements.Lists.Add(new ConditionList());
        foreach(var branch in requirements.Lists)
        {
            branch.Conditions.RemoveAll(c=>c is WeaponMasteryLevelCondition);
            if(category!=null) branch.Conditions.Add(CreateAutoRequirements(null,category,level).Conditions.Single());
        }
    }

    internal static void ApplyGeneratedItemTrading(ItemDescriptor item, bool marketplace=true) => item.CanSellInMarketplace=marketplace;
}
