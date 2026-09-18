using System.Drawing;
using System.Windows.Forms;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Editors.Events;

/// <summary>Edits a detached draft; Cancel never changes the command.</summary>
internal sealed class MiniGameCommandDialog : Form
{
    private sealed record AnimationChoice(Guid Id, string Name) { public override string ToString() => Name; }
    private sealed record CurrencyChoice(Guid Id, string Name) { public override string ToString() => Name; }

    public MiniGameCommandDialog(StartMiniGameCommand command)
    {
        Text = "Start Mini-Game - Poker"; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false; AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(660, Math.Min(740, Math.Max(480, (Screen.PrimaryScreen?.WorkingArea.Height ?? 900) - 140)));
        MinimumSize = new Size(580, 420);
        BackColor = DrawingColor.FromArgb(45, 45, 48); ForeColor = DrawingColor.Gainsboro;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 17,
            AutoScroll = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        for (var row = 0; row < 17; ++row) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 8, 12, 8),
        };
        Controls.Add(layout); Controls.Add(buttons);
        var hint = new Label
        {
            AutoSize = true, MaximumSize = new Size(590, 0), Margin = new Padding(3, 3, 3, 12),
            Text = "Same map instance + Table ID = shared table. Use identical settings on all access events. " +
                "Marlow deals and plays; Nora, Silas, Iris, Bastien and Oren fill guest seats. " +
                "25 XP per positive-net human win. B1-B6 unlock at levels 1/5/10/15/20/25.",
        };
        layout.Controls.Add(hint, 0, 0); layout.SetColumnSpan(hint, 2);
        var game = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        game.Items.Add("Poker - Texas hold'em"); game.SelectedIndex = 0;
        var table = new TextBox { Name = "TableId", Text = command.TableId ?? "", MaxLength = 64, Dock = DockStyle.Fill };
        var seats = Number(command.MaxPlayers, 2, 6);
        var currency = CurrencyPicker(command.CurrencyItemId);
        var chips = Number(command.StartingChips, 1, 1_000_000_000);
        var small = Number(command.SmallBlind, 1, 1_000_000_000);
        var big = Number(command.BigBlind, 1, 1_000_000_000);
        var seconds = Number(command.TurnSeconds, 5, 300);
        var dealer = new CheckBox { Text = "Marlow / Croupier", Checked = command.DealerPlays, AutoSize = true };
        var npcs = Number(command.NpcPlayers, 0, 5);
        var automatic = new CheckBox { Text = "Next hand after 5 seconds", Checked = command.AutoStart, AutoSize = true };
        var animation = AnimationPicker(command.DealAnimationId);
        var victory = AnimationPicker(command.VictoryAnimationId);
        var announce = new CheckBox { Text = "Human name + positive net win", Checked = command.AnnounceWins, AutoSize = true };
        var backs = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        for (var id = 0; id < MiniGameProgression.BackCount; ++id) backs.Items.Add($"B{id + 1} (B{id + 1}.png)");
        backs.SelectedIndex = Math.Clamp(command.NpcCardBackId, 0, MiniGameProgression.BackCount - 1);
        void LimitNpcs()
        {
            var maximum = seats.Value - 1 - (dealer.Checked ? 1 : 0);
            if (npcs.Value > maximum) npcs.Value = maximum;
            npcs.Maximum = maximum;
        }
        seats.ValueChanged += (_, _) => LimitNpcs(); dealer.CheckedChanged += (_, _) => LimitNpcs(); LimitNpcs();
        AddRow(layout, 1, "Mini-game", game);
        AddRow(layout, 2, "Table ID (letters, digits, - or _)", table);
        AddRow(layout, 3, "Maximum seats (humans + NPCs)", seats);
        AddRow(layout, 4, "Table currency / Monnaie", currency);
        var chipsLabel = AddRow(layout, 5, "Starting test chips", chips);
        AddRow(layout, 6, "Small blind", small); AddRow(layout, 7, "Big blind", big);
        AddRow(layout, 8, "Turn timeout (seconds)", seconds);
        AddRow(layout, 9, "Dealer plays and deals", dealer); AddRow(layout, 10, "Other NPC opponents", npcs);
        AddRow(layout, 11, "Automatic hands", automatic); AddRow(layout, 12, "Dealing animation", animation);
        AddRow(layout, 13, "Announce wins in GLOBAL chat", announce);
        AddRow(layout, 14, "Victory animation (winner only)", victory);
        AddRow(layout, 15, "Dealer / NPC card back", backs);
        var status = new Label { Name = "CurrencyStatus", AutoSize = true, MaximumSize = new Size(590, 0), Margin = new Padding(3, 12, 3, 12) };
        layout.Controls.Add(status, 0, 16); layout.SetColumnSpan(status, 2);
        void ShowCurrencyStatus()
        {
            var id = (currency.SelectedItem as CurrencyChoice)?.Id ?? Guid.Empty;
            if (id == Guid.Empty)
            {
                chipsLabel.Text = "Starting test chips";
                status.ForeColor = DrawingColor.Gainsboro;
                status.Text = "Playable TEST mode: no inventory items are taken or paid. " +
                    "Back up resources/minigames-test.db for test XP and card backs.";
                return;
            }
            chipsLabel.Text = "Planned buy-in (item units)";
            status.ForeColor = DrawingColor.Gold;
            var item = ItemDescriptor.Get(id);
            status.Text = !MiniGameCurrency.IsCompatible(item)
                ? "The selected item is missing or is no longer a compatible stackable item. " +
                    "Choose another item or explicitly choose test chips before saving."
                : $"Selected item: {item.Name}. ID: {id}.\n" +
                    "CONFIGURATION ONLY: inventory buy-in, settlement and refunds are not enabled in this build. " +
                    "Saving preserves this choice, but the server will refuse this configured table. " +
                    "Choose None / test chips to keep playing the test version.";
        }
        currency.SelectedIndexChanged += (_, _) => ShowCurrencyStatus(); ShowCurrencyStatus();
        var cancel = new Button { Name = "Cancel", Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var save = new Button { Name = "Save", Text = "Save", AutoSize = true };
        buttons.Controls.Add(cancel); buttons.Controls.Add(save);
        AcceptButton = save; CancelButton = cancel;
        save.Click += (_, _) =>
        {
            var selectedCurrency = (currency.SelectedItem as CurrencyChoice)?.Id ?? Guid.Empty;
            if (selectedCurrency != Guid.Empty && !MiniGameCurrency.IsCompatible(ItemDescriptor.Get(selectedCurrency)))
            {
                MessageBox.Show(this, "The selected item is missing or incompatible. Select a Currency or another stackable item.",
                    "Invalid table currency", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }
            var draft = new StartMiniGameCommand
            {
                Game = MiniGameType.Poker, TableId = table.Text, MaxPlayers = (int)seats.Value,
                CurrencyItemId = selectedCurrency,
                StartingChips = (long)chips.Value, SmallBlind = (long)small.Value, BigBlind = (long)big.Value,
                TurnSeconds = (int)seconds.Value, DealerPlays = dealer.Checked, NpcPlayers = (int)npcs.Value,
                AutoStart = automatic.Checked, DealAnimationId = ((AnimationChoice)animation.SelectedItem!).Id,
                AnnounceWins = announce.Checked, VictoryAnimationId = ((AnimationChoice)victory.SelectedItem!).Id,
                NpcCardBackId = backs.SelectedIndex,
            };
            if (!draft.HasValidSettings())
            {
                MessageBox.Show(this, "Use a valid Table ID and blinds. The starting amount must cover the big blind. Reserve one human seat.",
                    "Invalid poker table", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }
            command.Game = draft.Game; command.TableId = draft.TableId; command.MaxPlayers = draft.MaxPlayers;
            command.CurrencyItemId = draft.CurrencyItemId;
            command.StartingChips = draft.StartingChips; command.SmallBlind = draft.SmallBlind; command.BigBlind = draft.BigBlind;
            command.TurnSeconds = draft.TurnSeconds; command.DealerPlays = draft.DealerPlays; command.NpcPlayers = draft.NpcPlayers;
            command.AutoStart = draft.AutoStart; command.DealAnimationId = draft.DealAnimationId; command.AnnounceWins = draft.AnnounceWins;
            command.VictoryAnimationId = draft.VictoryAnimationId; command.NpcCardBackId = draft.NpcCardBackId;
            DialogResult = DialogResult.OK; Close();
        };
    }

    private static ComboBox CurrencyPicker(Guid id)
    {
        var picker = new ComboBox
        {
            Name = "TableCurrency", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill,
            DropDownWidth = 590, AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems,
        };
        picker.Items.Add(new CurrencyChoice(Guid.Empty, "None / Test chips (no inventory)"));
        foreach (var item in MiniGameCurrency.CompatibleItems(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()))
            picker.Items.Add(new CurrencyChoice(item.Id, MiniGameCurrency.DisplayName(item)));
        var selected = picker.Items.Cast<CurrencyChoice>().FirstOrDefault(choice => choice.Id == id);
        if (selected == null)
        {
            selected = new CurrencyChoice(id, "Missing / incompatible item: " + id);
            picker.Items.Add(selected);
        }
        picker.SelectedItem = selected;
        return picker;
    }

    private static ComboBox AnimationPicker(Guid id)
    {
        var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        picker.Items.Add(new AnimationChoice(Guid.Empty, "None / Aucune"));
        foreach (var item in AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            picker.Items.Add(new AnimationChoice(item.Id, item.Name));
        var selected = picker.Items.Cast<AnimationChoice>().FirstOrDefault(a => a.Id == id);
        if (selected == null) { selected = new AnimationChoice(id, "Missing animation: " + id); picker.Items.Add(selected); }
        picker.SelectedItem = selected; return picker;
    }
    private static NumericUpDown Number(long value, long minimum, long maximum) => new()
    { Minimum = minimum, Maximum = maximum, Value = Math.Clamp(value, minimum, maximum), DecimalPlaces = 0, ThousandsSeparator = true, Dock = DockStyle.Fill };
    private static Label AddRow(TableLayoutPanel layout, int row, string text, Control input)
    {
        var label = new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 8) };
        layout.Controls.Add(label, 0, row);
        input.Margin = new Padding(3, 6, 3, 6); layout.Controls.Add(input, 1, row);
        return label;
    }
}
