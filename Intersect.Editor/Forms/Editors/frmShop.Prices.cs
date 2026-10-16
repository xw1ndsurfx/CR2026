using DarkUI.Controls;
using Intersect.Framework.Core.GameObjects.Items;
namespace Intersect.Editor.Forms.Editors;
public partial class FrmShop
{
    private void InitializePriceSuggestions()
    {
        AddSuggestion(grpItemsSold, cmbAddSoldItem, cmbSellFor, nudSellCost, false);
        AddSuggestion(grpItemsBought, cmbAddBoughtItem, cmbBuyFor, nudBuyAmount, true);
    }
    private void AddSuggestion(Control group, ComboBox item, ComboBox currency, NumericUpDown cost, bool shopBuys)
    {
        cost.Width = 80;
        cost.Maximum = int.MaxValue;
        var button = new DarkButton { Left = cost.Right + 4, Top = cost.Top - 1, Width = 85, Height = 24, Text = "Suggest price" };
        group.Controls.Add(button);
        var tooltip = new ToolTip(components);
        int? Suggested() => ShopPriceSuggestion.Suggest(ItemDescriptor.FromList(item.SelectedIndex), ItemDescriptor.FromList(currency.SelectedIndex), shopBuys);
        void Refresh()
        {
            var price = Suggested();
            button.Enabled = price.HasValue && (!shopBuys || rdoBuyWhitelist.Checked);
            tooltip.SetToolTip(button, price.HasValue ? $"Suggested: {price:N0}. Item value / currency value" + (shopBuys ? " × 50% (shop buyback)." : ".") : "Set a positive item and currency value in Item Editor first. An item cannot be exchanged for itself.");
        }
        button.Click += (_, _) => { if (Suggested() is { } price) cost.Value = price; };
        item.SelectedIndexChanged += (_, _) => Refresh();
        currency.SelectedIndexChanged += (_, _) => Refresh();
        rdoBuyWhitelist.CheckedChanged += (_, _) => Refresh();
        Refresh();
    }
}
