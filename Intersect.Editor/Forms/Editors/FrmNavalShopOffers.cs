using DarkUI.Forms;
using Intersect.Framework.Core.Naval;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;
internal sealed class FrmNavalShopOffers:DarkForm
{
    private sealed record CurrencyChoice(Guid Id,string Name);
    private static string T(string text)=>NavalSetupText.Get(text,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    internal FrmNavalShopOffers(ShopDescriptor shop,NavalConfigurationPacket packet)
    {
        Text=T("Configure boats and skills");Size=new(900,620);StartPosition=FormStartPosition.CenterParent;
        var catalog=JsonConvert.DeserializeObject<HarbourCatalog>(packet.CatalogueJson)??new();
        var navigation=JsonConvert.DeserializeObject<NavalConfiguration>(packet.NavigationJson)??new();
        var offers=catalog.Offers.Select(model=>shop.NavalCatalogue.Offers.FirstOrDefault(o=>o.Id==model.Id)??model).Select(o=>JsonConvert.DeserializeObject<HarbourOffer>(JsonConvert.SerializeObject(o))!).ToArray();
        var skills=navigation.Skills.Select(d=>shop.NavalSkills.FirstOrDefault(s=>s.Kind==d.Kind)??d).Select(d=>JsonConvert.DeserializeObject<NavalSkillDefinition>(JsonConvert.SerializeObject(d))!).ToArray();
        var tabs=new TabControl {Dock=DockStyle.Fill};Controls.Add(tabs);
        var boats=Grid(tabs,"Ship shop and repairs");var abilities=Grid(tabs,"Sailing skills");
        foreach(var grid in new[]{boats,abilities})
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn {Name="Name",HeaderText=T("Name"),ReadOnly=true,Width=230});
            grid.Columns.Add(new DataGridViewCheckBoxColumn {Name="ForSale",HeaderText=T("For sale"),Width=65});
            grid.Columns.Add(new DataGridViewTextBoxColumn {Name="Price",HeaderText=T("Price"),Width=100});
            var currencies=ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i=>i.ItemType==ItemType.Currency).Select(i=>new CurrencyChoice(i.Id,i.Name)).Prepend(new(Guid.Empty,T("Default currency"))).ToList();
            grid.Columns.Add(new DataGridViewComboBoxColumn {Name="Currency",HeaderText=T("Currency"),Width=200,DataSource=currencies,ValueMember="Id",DisplayMember="Name"});
            grid.DataError+=(_,e)=>e.ThrowException=false;
        }
        boats.Columns.Add(new DataGridViewTextBoxColumn {Name="Repair",HeaderText=T("Full repair price"),Width=150});
        foreach(var offer in offers)boats.Rows.Add(offer.Name,offer.ForSale,offer.Price,offer.CurrencyItemId,offer.RepairPrice);
        foreach(var skill in skills)abilities.Rows.Add(T(NavalSkillDefinition.Caption(skill.Kind)),!shop.NavalCatalogue.Enabled || shop.NavalSkills.Any(s=>s.Kind==skill.Kind),skill.Price,skill.CurrencyItemId);
        var footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=42,FlowDirection=FlowDirection.RightToLeft};var save=new Button {Text=T("Save"),Width=120};var cancel=new Button {Text=T("Cancel"),Width=120};footer.Controls.AddRange([cancel,save]);Controls.Add(footer);
        cancel.Click+=(_,_)=>Close();save.Click+=(_,_)=>
        {
            boats.EndEdit();abilities.EndEdit();
            bool Price(DataGridViewRow row,string key,out int value)=>int.TryParse(row.Cells[key].Value?.ToString(),out value) && value is >=0 and <=1000000000;
            for(var i=0;i<offers.Length;i++)if(!Price(boats.Rows[i],"Price",out _) || !Price(boats.Rows[i],"Repair",out _))return;
            for(var i=0;i<skills.Length;i++)if(!Price(abilities.Rows[i],"Price",out _))return;
            for(var i=0;i<offers.Length;i++) {var row=boats.Rows[i];Price(row,"Price",out var price);Price(row,"Repair",out var repair);offers[i].ForSale=Convert.ToBoolean(row.Cells["ForSale"].Value);offers[i].Price=price;offers[i].RepairPrice=repair;offers[i].CurrencyItemId=row.Cells["Currency"].Value is Guid id?id:Guid.Empty;}
            for(var i=0;i<skills.Length;i++){var row=abilities.Rows[i];Price(row,"Price",out var price);skills[i].Price=price;skills[i].CurrencyItemId=row.Cells["Currency"].Value is Guid id?id:Guid.Empty;}
            shop.NavalCatalogue=new() {Enabled=true,Offers=offers};shop.NavalSkills=skills.Where((_,i)=>Convert.ToBoolean(abilities.Rows[i].Cells["ForSale"].Value)).ToArray();DialogResult=DialogResult.OK;Close();
        };
    }
    private static DataGridView Grid(TabControl tabs,string title)
    {
        var page=new TabPage(T(title));var grid=new DataGridView {Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};page.Controls.Add(grid);tabs.TabPages.Add(page);return grid;
    }
}
