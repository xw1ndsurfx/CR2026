using System.Drawing;
using System.Windows.Forms;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Editors.Events;

/// <summary>Edits a detached draft; the command changes only after a valid Save.</summary>
internal sealed class MiniGameCommandDialog : Form
{
    private sealed record AnimationChoice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    public MiniGameCommandDialog(StartMiniGameCommand command)
    {
        Text = "Start Mini-Game - Poker";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(580, 650);
        BackColor = DrawingColor.FromArgb(45, 45, 48);
        ForeColor = DrawingColor.Gainsboro;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 13,
            AutoScroll = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        for (var row = 0; row < 13; ++row) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);
        var hint = new Label
        {
            AutoSize = true, MaximumSize = new Size(530, 0), Margin = new Padding(3, 3, 3, 14),
            Text = "Same map instance + Table ID = shared table. Use identical settings on all access events. " +
                "The dealer is an opponent; the betting button still rotates. Other NPCs yield seats between hands. " +
                "TEST CHIPS ONLY: no Aureons are taken or paid. The selected animation plays in the poker window.",
        };
        layout.Controls.Add(hint, 0, 0);
        layout.SetColumnSpan(hint, 2);
        var game = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        game.Items.Add("Poker - Texas hold'em");
        game.SelectedIndex = 0;
        var table = new TextBox { Text = command.TableId ?? "", MaxLength = 64, Dock = DockStyle.Fill };
        var seats = Number(command.MaxPlayers, 2, 6);
        var chips = Number(command.StartingChips, 1, 1_000_000_000);
        var small = Number(command.SmallBlind, 1, 1_000_000_000);
        var big = Number(command.BigBlind, 1, 1_000_000_000);
        var seconds = Number(command.TurnSeconds, 5, 300);
        var dealer = new CheckBox { Text = "Dealer / Croupier", Checked = command.DealerPlays, AutoSize = true };
        var npcs = Number(command.NpcPlayers, 0, 5);
        var automatic = new CheckBox { Text = "Next hand after 5 seconds", Checked = command.AutoStart, AutoSize = true };
        var animation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        animation.Items.Add(new AnimationChoice(Guid.Empty, "None / Aucune"));
        foreach (var item in AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>()
                     .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            animation.Items.Add(new AnimationChoice(item.Id, item.Name));
        var selected = animation.Items.Cast<AnimationChoice>().FirstOrDefault(a => a.Id == command.DealAnimationId);
        if (selected == null)
        {
            selected = new AnimationChoice(command.DealAnimationId, "Missing animation: " + command.DealAnimationId);
            animation.Items.Add(selected); // Do not silently replace a deleted asset on Cancel/Save.
        }
        animation.SelectedItem = selected;
        void LimitNpcs()
        {
            var maximum = seats.Value - 1 - (dealer.Checked ? 1 : 0);
            if (npcs.Value > maximum) npcs.Value = maximum;
            npcs.Maximum = maximum;
        }
        seats.ValueChanged += (_, _) => LimitNpcs();
        dealer.CheckedChanged += (_, _) => LimitNpcs();
        LimitNpcs();
        AddRow(layout, 1, "Mini-game", game);
        AddRow(layout, 2, "Table ID (letters, digits, - or _)", table);
        AddRow(layout, 3, "Maximum seats (humans + NPCs)", seats);
        AddRow(layout, 4, "Starting test chips", chips);
        AddRow(layout, 5, "Small blind", small);
        AddRow(layout, 6, "Big blind", big);
        AddRow(layout, 7, "Turn timeout (seconds)", seconds);
        AddRow(layout, 8, "Dealer plays and deals", dealer);
        AddRow(layout, 9, "Other NPC opponents", npcs);
        AddRow(layout, 10, "Automatic hands", automatic);
        AddRow(layout, 11, "Dealing animation", animation);
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(3, 14, 3, 3),
        };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Save", AutoSize = true };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        layout.Controls.Add(buttons, 0, 12);
        layout.SetColumnSpan(buttons, 2);
        AcceptButton = save;
        CancelButton = cancel;
        save.Click += (_, _) =>
        {
            var draft = new StartMiniGameCommand
            {
                Game = MiniGameType.Poker, TableId = table.Text, MaxPlayers = (int)seats.Value,
                StartingChips = (long)chips.Value, SmallBlind = (long)small.Value,
                BigBlind = (long)big.Value, TurnSeconds = (int)seconds.Value,
                DealerPlays = dealer.Checked, NpcPlayers = (int)npcs.Value, AutoStart = automatic.Checked,
                DealAnimationId = ((AnimationChoice)animation.SelectedItem!).Id,
            };
            if (!draft.HasValidSettings())
            {
                MessageBox.Show(this,
                    "Use a Table ID of 1-64 letters, digits, hyphens or underscores. Starting chips must cover " +
                    "the big blind, and the big blind must cover the small blind. Reserve at least one human seat.",
                    "Invalid poker table", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            command.Game = draft.Game;
            command.TableId = draft.TableId;
            command.MaxPlayers = draft.MaxPlayers;
            command.StartingChips = draft.StartingChips;
            command.SmallBlind = draft.SmallBlind;
            command.BigBlind = draft.BigBlind;
            command.TurnSeconds = draft.TurnSeconds;
            command.DealerPlays = draft.DealerPlays;
            command.NpcPlayers = draft.NpcPlayers;
            command.AutoStart = draft.AutoStart;
            command.DealAnimationId = draft.DealAnimationId;
            DialogResult = DialogResult.OK;
            Close();
        };
    }

    private static NumericUpDown Number(long value, long minimum, long maximum) => new()
    {
        Minimum = minimum, Maximum = maximum, Value = Math.Clamp(value, minimum, maximum),
        DecimalPlaces = 0, ThousandsSeparator = true, Dock = DockStyle.Fill,
    };
    private static void AddRow(TableLayoutPanel layout, int row, string text, Control input)
    {
        layout.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 3, 8) }, 0, row);
        input.Margin = new Padding(3, 6, 3, 6);
        layout.Controls.Add(input, 1, row);
    }
}
