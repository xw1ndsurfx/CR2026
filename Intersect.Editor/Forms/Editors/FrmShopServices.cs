using System.ComponentModel;
using DarkUI.Forms;
using Intersect.Editor.Content;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;
internal sealed class FrmShopServices:DarkForm
{
    internal FrmShopServices(ShopDescriptor shop)
    {
        string T(string text)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(text,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        Text=T(shop.ConfigurationKind==ShopConfigurationKind.Spells?"Spell shop":"Premium music shop");
        Size=new Size(1100,650);MinimumSize=new Size(850,500);StartPosition=FormStartPosition.CenterParent;
        var draft=new BindingList<ShopServiceOffer>(JsonConvert.DeserializeObject<List<ShopServiceOffer>>(JsonConvert.SerializeObject(shop.ServiceOffers))??[]);
        var grid=new DataGridView{Dock=DockStyle.Fill,AutoGenerateColumns=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,DataSource=draft,Name="ServiceOffersGrid",AllowUserToAddRows=true,AllowUserToDeleteRows=true};
        grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=T("Name"),DataPropertyName="Name"});
        if(shop.ConfigurationKind==ShopConfigurationKind.Spells)
        {
            var spells=new[]{new {Id=Guid.Empty,Name=T("None")}}.Concat(SpellDescriptor.Lookup.Values.OfType<SpellDescriptor>().Select(spell=>new{spell.Id,spell.Name})).ToArray();
            grid.Columns.Add(new DataGridViewComboBoxColumn{HeaderText=T("Spell"),DataPropertyName="SpellId",DataSource=spells,DisplayMember="Name",ValueMember="Id"});
        }
        else grid.Columns.Add(new DataGridViewComboBoxColumn{HeaderText=T("Music"),DataPropertyName="Music",DataSource=new[]{""}.Concat(GameContentManager.SmartSortedMusicNames??[]).ToArray()});
        var items=new[]{new {Id=Guid.Empty,Name=T("Shop currency")}}.Concat(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(item=>item.ItemType==ItemType.Currency).Select(item=>new{item.Id,item.Name})).ToArray();
        grid.Columns.Add(new DataGridViewComboBoxColumn{HeaderText=T("Currency"),DataPropertyName="CurrencyItemId",DataSource=items,DisplayMember="Name",ValueMember="Id"});
        foreach(var pair in new[]{("Price","Price"),("Required level","RequiredLevel"),("Duration (seconds)","DurationSeconds"),("Icon","Icon")})
            grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=T(pair.Item1),DataPropertyName=pair.Item2});
        grid.DataError+=(_,e)=>e.ThrowException=false;
        grid.CellEndEdit+=(_,e)=>
        {
            if(e.RowIndex<0 || grid.Rows[e.RowIndex].DataBoundItem is not ShopServiceOffer row)return;
            if(shop.ConfigurationKind==ShopConfigurationKind.Spells && SpellDescriptor.Get(row.SpellId) is {} spell)
            {if(row.Name.Length==0)row.Name=spell.Name;if(row.Icon.Length==0)row.Icon=spell.Icon;}
        };
        var save=new Button{Dock=DockStyle.Bottom,Height=40,Text=T("Save"),Name="SaveServiceOffers"};
        save.Click+=(_,_)=>
        {
            grid.EndEdit();
            if(draft.Count>256 || draft.Any(row=>!row.IsValid || (shop.ConfigurationKind==ShopConfigurationKind.Spells?SpellDescriptor.Get(row.SpellId)==null:string.IsNullOrWhiteSpace(row.Music))))
            {MessageBox.Show(this,T("Check the shop offers and prices."));return;}
            shop.ServiceOffers=draft.ToArray();DialogResult=DialogResult.OK;Close();
        };
        Controls.Add(grid);Controls.Add(save);
    }
}
