using System.Drawing;
using System.Windows.Forms;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Editors.Events;

/// <summary>Edits a detached draft; the event command is changed only after a valid Save.</summary>
internal sealed class MiniGameCommandDialog : Form
{
    public MiniGameCommandDialog(StartMiniGameCommand command)
    {
        Text = "Start Mini-Game - Poker";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(540, 430);
        BackColor = DrawingColor.FromArgb(45, 45, 48);
        ForeColor = DrawingColor.Gainsboro;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 9,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        for (var row = 0; row < 9; ++row) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);
        var hint = new Label
        {
            AutoSize = true, MaximumSize = new Size(500, 0), Margin = new Padding(3, 3, 3, 14),
            Text = "Players using the same Table ID on the same map instance share one table. " +
                "Use identical settings on those events. This version uses temporary test chips only; " +
                "the playable client window is not included yet.",
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
        AddRow(layout, 1, "Mini-game", game);
        AddRow(layout, 2, "Table ID (letters, digits, - or _)", table);
        AddRow(layout, 3, "Maximum players", seats);
        AddRow(layout, 4, "Starting test chips", chips);
        AddRow(layout, 5, "Small blind", small);
        AddRow(layout, 6, "Big blind", big);
        AddRow(layout, 7, "Turn timeout (seconds)", seconds);
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(3, 14, 3, 3),
        };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Save", AutoSize = true };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        layout.Controls.Add(buttons, 0, 8);
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
            };
            if (!draft.HasValidSettings())
            {
                MessageBox.Show(this,
                    "Use a Table ID of 1-64 letters, digits, hyphens or underscores (no spaces). " +
                    "Starting chips must cover the big blind, and the big blind must be at least the small blind.",
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
