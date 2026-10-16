using DarkUI.Controls;
using Intersect.Framework.Core.GameObjects.Items;
namespace Intersect.Editor.Forms.Editors;
public partial class FrmShop
{
    private void InitializePriceSuggestions()
    {
        // Reserve a separate row: translated labels and DPI scaling must not clip the action.
        const int extra = 36;
        Height += extra;
        grpShops.Height += extra;
        lstGameObjects.Height += extra;
        pnlContainer.Height += extra;
        foreach (var group in new[] { grpItemsSold, grpItemsBought })
        {
            foreach (Control child in group.Controls)
                if (child.Top > nudSellCost.Bottom) child.Top += extra;
            group.Height += extra;
        }
        btnSave.Top += extra;
        btnCancel.Top += extra;
        AddSuggestion(grpItemsSold, cmbAddSoldItem, cmbSellFor, nudSellCost, false);
        AddSuggestion(grpItemsBought, cmbAddBoughtItem, cmbBuyFor, nudBuyAmount, true);
    }
    private void AddSuggestion(Control group, ComboBox item, ComboBox currency, NumericUpDown cost, bool shopBuys)
    {
        cost.Maximum = int.MaxValue;
        var button = new DarkButton { Name = shopBuys ? "SuggestBuyPrice" : "SuggestSellPrice", Left = 8, Top = cost.Bottom + 5, Width = group.ClientSize.Width - 16, Height = 28, Text = "Suggest price" };
        group.Controls.Add(button);
        var tooltip = new ToolTip(components);
        int? Suggested() => ShopPriceSuggestion.Suggest(ItemDescriptor.FromList(item.SelectedIndex), ItemDescriptor.FromList(currency.SelectedIndex), shopBuys);
        void Refresh()
        {
            var price = Suggested();
            button.Enabled = price.HasValue && (!shopBuys || rdoBuyWhitelist.Checked);
            var conversion = ItemDescriptor.FromList(currency.SelectedIndex)?.Price > 0
                ? "Item value / currency value" : "Currency conversion is unset: using the item's base price in currency units";
            tooltip.SetToolTip(button, shopBuys && !rdoBuyWhitelist.Checked ? "Buyback prices apply only in whitelist mode." : price.HasValue ? $"Suggested: {price:N0}. {conversion}" + (shopBuys ? " × 50% (shop buyback)." : ".") : "Select an item with a positive price in Item Editor and a currency. An item cannot be exchanged for itself.");
        }
        button.Click += (_, _) => { if (Suggested() is { } price) cost.Value = price; };
        item.SelectedIndexChanged += (_, _) => Refresh();
        currency.SelectedIndexChanged += (_, _) => Refresh();
        rdoBuyWhitelist.CheckedChanged += (_, _) => Refresh();
        Refresh();
    }
}
