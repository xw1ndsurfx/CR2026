using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
namespace Intersect.Editor.Forms.Editors;
public sealed partial class FrmJoustConfiguration
{
    private sealed record OfferChoice(JoustOffer Offer) { public override string ToString() => $"{ItemDescriptor.GetName(Offer.ItemId)} ×{Offer.Quantity} · {Offer.Medals} {T("Royal medals")}"; }
    private void BuildEconomy(TabControl tabs)
    {
        var page = Page(tabs, "4. Rewards and economy");
        var enabled = new CheckBox { Name = "EconomyEnabled", Text = T("Enable royal medal rewards"), AutoSize = true, Checked = _config.Economy.Enabled };
        enabled.CheckedChanged += (_, _) => { _config.Economy.Enabled = enabled.Checked; Changed(); }; Row(page, "", enabled);
        var cap = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = _config.Economy.DailyMedalLimit, Dock = DockStyle.Fill };
        cap.ValueChanged += (_, _) => { _config.Economy.DailyMedalLimit = (int)cap.Value; Changed(); }; Row(page, "Daily medal limit (UTC)", cap);
        var cooldown = new NumericUpDown { Minimum = 0, Maximum = 86400, Value = _config.Economy.RewardCooldownSeconds, Dock = DockStyle.Fill };
        cooldown.ValueChanged += (_, _) => { _config.Economy.RewardCooldownSeconds = (int)cooldown.Value; Changed(); }; Row(page, "Reward cooldown (seconds)", cooldown);
        Row(page, "", new Label { AutoSize = true, MaximumSize = new(320, 0), Text = T("A victory awards 5 to 25 medals depending on AI difficulty; a completed loss awards 1. At least one scored hit is required. Abandoned matches award nothing. Medals and items are saved together when exchanged.") });
        var list = new ListBox { Height = 150, Dock = DockStyle.Fill, Name = "JoustRewardOffers" };
        void Reload() { list.Items.Clear(); list.Items.AddRange(_config.Economy.Offers.Select(o => (object)new OfferChoice(o)).ToArray()); }
        Reload(); Row(page, "Medal shop offers", list);
        var items = new ComboBox { Name = "JoustRewardItem", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        items.Items.AddRange(MiniGameCurrency.CompatibleItems(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()).Select(i => (object)new Choice(i.Id, i.Name)).ToArray());
        if (items.Items.Count > 0) items.SelectedIndex = 0; Row(page, "Reward item", items);
        var quantity = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = 1, Dock = DockStyle.Fill }; Row(page, "Quantity", quantity);
        var price = new NumericUpDown { Minimum = 1, Maximum = 100000, Value = 10, Dock = DockStyle.Fill }; Row(page, "Price in royal medals", price);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        AddButton(actions, "Add shop offer", () => { if (items.SelectedItem is not Choice c || _config.Economy.Offers.Length >= 64) return;
            _config.Economy.Offers = [.._config.Economy.Offers, new JoustOffer { ItemId = c.Id, Quantity = (int)quantity.Value, Medals = (int)price.Value }]; Reload(); Changed(); });
        AddButton(actions, "Remove shop offer", () => { if (list.SelectedItem is not OfferChoice c) return;
            _config.Economy.Offers = _config.Economy.Offers.Where(o => o.Id != c.Offer.Id).ToArray(); Reload(); Changed(); });
        Row(page, "", actions);
    }
}
