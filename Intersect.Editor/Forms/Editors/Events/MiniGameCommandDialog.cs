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
    private sealed record SoundChoice(string File, string Name) { public override string ToString() => Name; }

    public MiniGameCommandDialog(StartMiniGameCommand command)
    {
        Text = "Start Mini-Game - Poker"; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false; AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(720, Math.Min(800, Math.Max(520, (Screen.PrimaryScreen?.WorkingArea.Height ?? 900) - 100)));
        MinimumSize = new Size(620, 460);
        BackColor = DrawingColor.FromArgb(45, 45, 48); ForeColor = DrawingColor.Gainsboro;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 48, AutoScroll = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        for (var i = 0; i < 48; ++i) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 8, 12, 8) };
        Controls.Add(layout); Controls.Add(buttons);

        var row = 0;
        var hint = new Label { AutoSize = true, MaximumSize = new Size(650, 0), Margin = new Padding(3, 3, 3, 12),
            Text = "Same map instance + Table ID = shared table. All access events must use identical settings. " +
                "Animations are local screen effects; an Animation's own Sound is also played. Direct action sounds are optional WAV files from resources/sounds." };
        layout.Controls.Add(hint, 0, row++); layout.SetColumnSpan(hint, 2);

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
        var unlimited = new CheckBox { Text = "Unlimited NPC bankroll (economy faucet)", Checked = command.UnlimitedNpcFunds, AutoSize = true };
        var reserve = Number(command.NpcReserve, 0, 1_000_000_000); reserve.Name = "NpcReserve";
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

        AddRow(layout, row++, "Mini-game", game);
        AddRow(layout, row++, "Table ID (letters, digits, - or _)", table);
        AddRow(layout, row++, "Maximum seats (humans + NPCs)", seats);
        AddRow(layout, row++, "Table currency / Monnaie", currency);
        var chipsLabel = AddRow(layout, row++, "Starting test chips", chips);
        AddRow(layout, row++, "Small blind", small); AddRow(layout, row++, "Big blind", big);
        AddRow(layout, row++, "Turn timeout (seconds)", seconds);
        AddRow(layout, row++, "Dealer plays and deals", dealer); AddRow(layout, row++, "Other NPC opponents", npcs);
        AddRow(layout, row++, "Automatic hands", automatic);
        AddRow(layout, row++, "Unlimited NPC bankroll", unlimited);
        AddRow(layout, row++, "Initial NPC reserve (finite mode only)", reserve);
        AddRow(layout, row++, "Dealer / NPC card back", backs);
        AddRow(layout, row++, "Announce human wins in GLOBAL chat", announce);

        var dealAnim = AnimationPicker(command.DealAnimationId); var dealSound = SoundPicker(command.DealSound);
        var checkAnim = AnimationPicker(command.CheckAnimationId); var checkSound = SoundPicker(command.CheckSound);
        var callAnim = AnimationPicker(command.CallAnimationId); var callSound = SoundPicker(command.CallSound);
        var raiseAnim = AnimationPicker(command.RaiseAnimationId); var raiseSound = SoundPicker(command.RaiseSound);
        var foldAnim = AnimationPicker(command.FoldAnimationId); var foldSound = SoundPicker(command.FoldSound);
        var allInAnim = AnimationPicker(command.AllInAnimationId); var allInSound = SoundPicker(command.AllInSound);
        var showdownAnim = AnimationPicker(command.ShowdownAnimationId); var showdownSound = SoundPicker(command.ShowdownSound);
        var turnAnim = AnimationPicker(command.TurnAnimationId); var turnSound = SoundPicker(command.TurnSound);
        var victoryAnim = AnimationPicker(command.VictoryAnimationId); var victorySound = SoundPicker(command.VictorySound);
        var defeatAnim = AnimationPicker(command.DefeatAnimationId); var defeatSound = SoundPicker(command.DefeatSound);
        var leaveAnim = AnimationPicker(command.LeaveAnimationId); var leaveSound = SoundPicker(command.LeaveSound);

        AddSection(layout, row++, "Poker action effects (None is allowed)");
        AddRow(layout, row++, "Deal animation", dealAnim); AddRow(layout, row++, "Deal sound", dealSound);
        AddRow(layout, row++, "Check animation", checkAnim); AddRow(layout, row++, "Check sound", checkSound);
        AddRow(layout, row++, "Call animation", callAnim); AddRow(layout, row++, "Call sound", callSound);
        AddRow(layout, row++, "Raise animation", raiseAnim); AddRow(layout, row++, "Raise sound", raiseSound);
        AddRow(layout, row++, "Fold animation", foldAnim); AddRow(layout, row++, "Fold sound", foldSound);
        AddRow(layout, row++, "All-in animation", allInAnim); AddRow(layout, row++, "All-in sound", allInSound);
        AddRow(layout, row++, "Showdown animation", showdownAnim); AddRow(layout, row++, "Showdown sound", showdownSound);
        AddRow(layout, row++, "Your-turn animation", turnAnim); AddRow(layout, row++, "Your-turn sound", turnSound);
        AddRow(layout, row++, "Victory animation (winner only)", victoryAnim); AddRow(layout, row++, "Victory sound", victorySound);
        AddRow(layout, row++, "Defeat/no-profit animation", defeatAnim); AddRow(layout, row++, "Defeat/no-profit sound", defeatSound);
        AddRow(layout, row++, "Leave animation", leaveAnim); AddRow(layout, row++, "Leave sound", leaveSound);

        var status = new Label { Name = "CurrencyStatus", AutoSize = true, MaximumSize = new Size(650, 0), Margin = new Padding(3, 12, 3, 12) };
        layout.Controls.Add(status, 0, row++); layout.SetColumnSpan(status, 2);

        void ShowCurrencyStatus()
        {
            var id = (currency.SelectedItem as CurrencyChoice)?.Id ?? Guid.Empty;
            unlimited.Enabled = id != Guid.Empty;
            if (id == Guid.Empty) unlimited.Checked = false;
            reserve.Enabled = id != Guid.Empty && !unlimited.Checked;
            if (id == Guid.Empty)
            {
                chipsLabel.Text = "Starting test chips"; status.ForeColor = DrawingColor.Gainsboro;
                status.Text = "TEST mode: no inventory items are taken or paid. NPCs already refill with test chips.";
                return;
            }
            chipsLabel.Text = "Buy-in (inventory item units)"; status.ForeColor = unlimited.Checked ? DrawingColor.OrangeRed : DrawingColor.Gold;
            var item = ItemDescriptor.Get(id);
            status.Text = !MiniGameCurrency.IsCompatible(item)
                ? "The selected item is missing or incompatible."
                : unlimited.Checked
                    ? $"Selected item: {item.Name}. UNLIMITED NPC BANKROLL: NPC stakes are created as needed. " +
                      "Human winnings therefore create new units of this currency. Use only when this economy faucet is intentional."
                    : $"Selected item: {item.Name}. FINITE NPC BANKROLL: the one-time reserve funds opponents and does not refill automatically.";
        }
        currency.SelectedIndexChanged += (_, _) => ShowCurrencyStatus();
        unlimited.CheckedChanged += (_, _) => ShowCurrencyStatus(); ShowCurrencyStatus();

        var cancel = new Button { Name = "Cancel", Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var save = new Button { Name = "Save", Text = "Save", AutoSize = true };
        buttons.Controls.Add(cancel); buttons.Controls.Add(save); AcceptButton = save; CancelButton = cancel;
        save.Click += (_, _) =>
        {
            var selected = (currency.SelectedItem as CurrencyChoice)?.Id ?? Guid.Empty;
            if (selected != Guid.Empty && !MiniGameCurrency.IsCompatible(ItemDescriptor.Get(selected)))
            {
                MessageBox.Show(this, "The selected item is missing or incompatible.", "Invalid table currency",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }
            var draft = new StartMiniGameCommand
            {
                Game = MiniGameType.Poker, TableId = table.Text, MaxPlayers = (int)seats.Value, CurrencyItemId = selected,
                StartingChips = (long)chips.Value, NpcReserve = (long)reserve.Value, UnlimitedNpcFunds = unlimited.Checked,
                SmallBlind = (long)small.Value, BigBlind = (long)big.Value, TurnSeconds = (int)seconds.Value,
                DealerPlays = dealer.Checked, NpcPlayers = (int)npcs.Value, AutoStart = automatic.Checked,
                DealAnimationId = A(dealAnim), AnnounceWins = announce.Checked, VictoryAnimationId = A(victoryAnim),
                NpcCardBackId = backs.SelectedIndex, CheckAnimationId = A(checkAnim), CallAnimationId = A(callAnim),
                RaiseAnimationId = A(raiseAnim), FoldAnimationId = A(foldAnim), AllInAnimationId = A(allInAnim),
                ShowdownAnimationId = A(showdownAnim), TurnAnimationId = A(turnAnim), DefeatAnimationId = A(defeatAnim),
                LeaveAnimationId = A(leaveAnim), DealSound = S(dealSound), CheckSound = S(checkSound),
                CallSound = S(callSound), RaiseSound = S(raiseSound), FoldSound = S(foldSound), AllInSound = S(allInSound),
                ShowdownSound = S(showdownSound), TurnSound = S(turnSound), VictorySound = S(victorySound),
                DefeatSound = S(defeatSound), LeaveSound = S(leaveSound),
            };
            if (!draft.HasValidSettings())
            {
                MessageBox.Show(this, "Use a valid Table ID, seats and blinds. The starting amount must cover the big blind.",
                    "Invalid poker table", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }
            Copy(draft, command); DialogResult = DialogResult.OK; Close();
        };
    }

    private static void Copy(StartMiniGameCommand d, StartMiniGameCommand c)
    {
        c.Game=d.Game;c.TableId=d.TableId;c.MaxPlayers=d.MaxPlayers;c.CurrencyItemId=d.CurrencyItemId;c.StartingChips=d.StartingChips;
        c.NpcReserve=d.NpcReserve;c.UnlimitedNpcFunds=d.UnlimitedNpcFunds;c.SmallBlind=d.SmallBlind;c.BigBlind=d.BigBlind;c.TurnSeconds=d.TurnSeconds;
        c.DealerPlays=d.DealerPlays;c.NpcPlayers=d.NpcPlayers;c.AutoStart=d.AutoStart;c.DealAnimationId=d.DealAnimationId;
        c.AnnounceWins=d.AnnounceWins;c.VictoryAnimationId=d.VictoryAnimationId;c.NpcCardBackId=d.NpcCardBackId;
        c.CheckAnimationId=d.CheckAnimationId;c.CallAnimationId=d.CallAnimationId;c.RaiseAnimationId=d.RaiseAnimationId;
        c.FoldAnimationId=d.FoldAnimationId;c.AllInAnimationId=d.AllInAnimationId;c.ShowdownAnimationId=d.ShowdownAnimationId;
        c.TurnAnimationId=d.TurnAnimationId;c.DefeatAnimationId=d.DefeatAnimationId;c.LeaveAnimationId=d.LeaveAnimationId;
        c.DealSound=d.DealSound;c.CheckSound=d.CheckSound;c.CallSound=d.CallSound;c.RaiseSound=d.RaiseSound;c.FoldSound=d.FoldSound;
        c.AllInSound=d.AllInSound;c.ShowdownSound=d.ShowdownSound;c.TurnSound=d.TurnSound;c.VictorySound=d.VictorySound;
        c.DefeatSound=d.DefeatSound;c.LeaveSound=d.LeaveSound;
    }
    private static Guid A(ComboBox picker) => ((AnimationChoice)picker.SelectedItem!).Id;
    private static string S(ComboBox picker) => ((SoundChoice)picker.SelectedItem!).File;

    private static ComboBox CurrencyPicker(Guid id)
    {
        var picker = new ComboBox { Name = "TableCurrency", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 590 };
        picker.Items.Add(new CurrencyChoice(Guid.Empty, "None / Test chips (no inventory)"));
        foreach (var item in MiniGameCurrency.CompatibleItems(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()))
            picker.Items.Add(new CurrencyChoice(item.Id, MiniGameCurrency.DisplayName(item)));
        var selected = picker.Items.Cast<CurrencyChoice>().FirstOrDefault(choice => choice.Id == id);
        if (selected == null) { selected = new CurrencyChoice(id, "Missing / incompatible item: " + id); picker.Items.Add(selected); }
        picker.SelectedItem = selected; return picker;
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
    private static ComboBox SoundPicker(string? file)
    {
        var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 500 };
        picker.Items.Add(new SoundChoice("", "None / Aucun"));
        foreach (var sound in Intersect.Editor.Content.ContentManager.SmartSortedSoundNames ?? Array.Empty<string>())
            picker.Items.Add(new SoundChoice(sound, sound));
        var selected = picker.Items.Cast<SoundChoice>().FirstOrDefault(s => string.Equals(s.File, file ?? "", StringComparison.OrdinalIgnoreCase));
        if (selected == null) { selected = new SoundChoice(file ?? "", "Missing sound: " + file); picker.Items.Add(selected); }
        picker.SelectedItem = selected; return picker;
    }
    private static NumericUpDown Number(long value, long minimum, long maximum) => new()
    { Minimum = minimum, Maximum = maximum, Value = Math.Clamp(value, minimum, maximum), DecimalPlaces = 0, ThousandsSeparator = true, Dock = DockStyle.Fill };
    private static Label AddRow(TableLayoutPanel layout, int row, string text, Control input)
    {
        var label = new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 8) };
        layout.Controls.Add(label, 0, row); input.Margin = new Padding(3, 6, 3, 6); layout.Controls.Add(input, 1, row); return label;
    }
    private static void AddSection(TableLayoutPanel layout, int row, string text)
    {
        var label = new Label { Text = text, AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
            ForeColor = DrawingColor.Gold, Margin = new Padding(3, 14, 3, 8) };
        layout.Controls.Add(label, 0, row); layout.SetColumnSpan(label, 2);
    }
}
