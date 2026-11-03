using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors;
public sealed class FrmMasteryRespec : Form
{
    private readonly CheckBox _enabled = new() { AutoSize = true };
    private readonly ComboBox _currency = new() { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name" };
    private readonly NumericUpDown _cost = new() { Minimum = 1, Maximum = 100000000 };
    public bool RespecEnabled => _enabled.Checked;
    public Guid CurrencyId => (_currency.SelectedItem as ItemDescriptor)?.Id ?? Guid.Empty;
    public int Cost => (int)_cost.Value;
    private static string T(string source) => QualityOfLifeText.Get(source, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    public FrmMasteryRespec(RoyalCombatConfiguration config)
    {
        Text = T("Mastery reset settings"); ClientSize = new(500, 240); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        _enabled.Text = T("Enable paid mastery resets"); _enabled.Checked = config.RespecEnabled; _enabled.SetBounds(16, 16, 468, 28);
        var currencyLabel = new Label { Text = T("Existing currency item"), AutoSize = true }; currencyLabel.SetBounds(16, 54, 468, 22);
        _currency.SetBounds(16, 80, 468, 28);
        _currency.FormattingEnabled = true;
        _currency.Format += (_, e) => { if (e.ListItem is ItemDescriptor item) e.Value = MiniGameCurrency.DisplayName(item); };
        foreach (var item in MiniGameCurrency.CompatibleItems(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>())) _currency.Items.Add(item);
        _currency.SelectedItem = _currency.Items.Cast<ItemDescriptor>().FirstOrDefault(i => i.Id == config.RespecCurrencyId);
        var costLabel = new Label { Text = T("Cost per mastery category"), AutoSize = true }; costLabel.SetBounds(16, 116, 468, 22);
        _cost.Value = Math.Clamp(config.RespecCost, 1, 100000000); _cost.SetBounds(16, 142, 468, 28);
        var save = new Button { Text = T("Save") }; save.SetBounds(16, 190, 226, 32);
        var note = new Label { Text = T("Choose a currency to enable resets."), AutoSize = true }; note.SetBounds(16, 172, 468, 18);
        void ValidateSelection() { var valid = !RespecEnabled || CurrencyId != Guid.Empty; save.Enabled = valid; note.Visible = !valid; }
        _enabled.CheckedChanged += (_, _) => ValidateSelection(); _currency.SelectedIndexChanged += (_, _) => ValidateSelection(); ValidateSelection();
        var cancel = new Button { Text = T("Cancel"), DialogResult = DialogResult.Cancel }; cancel.SetBounds(258, 190, 226, 32);
        save.Click += (_, _) => { if (RespecEnabled && CurrencyId == Guid.Empty) return; DialogResult = DialogResult.OK; Close(); };
        Controls.AddRange([_enabled, currencyLabel, _currency, costLabel, _cost, note, save, cancel]); AcceptButton = save; CancelButton = cancel;
    }
}
