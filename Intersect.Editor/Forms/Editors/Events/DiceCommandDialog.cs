using System.Globalization;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Configuration;
using Intersect.Framework.Core.QualityOfLife;

namespace Intersect.Editor.Forms.Editors.Events;

internal sealed class DiceCommandDialog : Form
{
    private sealed record CurrencyChoice(Guid Id, string Name) { public override string ToString() => Name; }
    private static string T(string s) => QualityOfLifeText.Get(s, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    public DiceCommandDialog(StartMiniGameCommand command) => Configure(this, command);
    internal static void Configure(Form form, StartMiniGameCommand command)
    {
        form.Text = "Roll the Dice"; form.Size = new Size(690, 650); form.MinimumSize = new Size(560, 480);
        form.StartPosition = FormStartPosition.CenterParent;
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(16) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        var row = 0;
        void Add(string caption, Control control)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(new Label { Text = T(caption), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 9, 3, 9) }, 0, row);
            control.Dock = DockStyle.Fill; grid.Controls.Add(control, 1, row++);
        }
        NumericUpDown Number(long value, long min, long max) => new() { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max) };
        var table = new TextBox { Text = command.Game == MiniGameType.RollTheDice ? command.TableId : "dice-1", MaxLength = 64 };
        var capacity = Number(command.MaxPlayers, 1, 6);
        var currency = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var item in MiniGameCurrency.CompatibleItems(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()))
            currency.Items.Add(new CurrencyChoice(item.Id, item.Name));
        currency.SelectedItem = currency.Items.Cast<CurrencyChoice>().FirstOrDefault(c => c.Id == command.CurrencyItemId);
        var buyIn = Number(command.StartingChips, 1, 1_000_000_000);
        var reserve = Number(command.NpcReserve, 0, 1_000_000_000);
        var minimum = Number(command.DiceMinimumBet, 1, 20_000_000);
        var maximum = Number(command.DiceMaximumBet, 1, 20_000_000);
        var step = Number(command.DiceBetStep, 1, 20_000_000);
        var outside = Number(command.DiceOutsideReturn, 2, 50);
        var seven = Number(command.DiceSevenReturn, 2, 50);
        Add("Table ID", table); Add("Maximum players", capacity); Add("Currency item (Aureons)", currency);
        Add("Buy-in reserved from inventory", buyIn); Add("House reserve per player", reserve);
        Add("Minimum wager", minimum); Add("Maximum wager", maximum); Add("Wager step", step);
        Add("Total return for less/more than 7", outside); Add("Total return for exactly 7", seven);
        var help = new Label { Text = T("Use the same table ID and settings on the same map instance. Each player rolls independently. The lobby recruits players; Meet host does not buy in automatically."),
            AutoSize = true, MaximumSize = new Size(610, 0), Margin = new Padding(3, 15, 3, 10) };
        grid.Controls.Add(help, 0, row++); grid.SetColumnSpan(help, 2);
        var error = new Label { AutoSize = true, ForeColor = System.Drawing.Color.Firebrick, MaximumSize = new Size(600, 0) };
        grid.Controls.Add(error, 0, row++); grid.SetColumnSpan(error, 2);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = T("Cancel"), DialogResult = DialogResult.Cancel, Width = 115 };
        var save = new Button { Text = T("Save"), Width = 115 };
        save.Click += (_, _) =>
        {
            var draft = new StartMiniGameCommand { Game = MiniGameType.RollTheDice, TableId = table.Text.Trim(),
                MaxPlayers = (int)capacity.Value, CurrencyItemId = (currency.SelectedItem as CurrencyChoice)?.Id ?? Guid.Empty,
                StartingChips = (long)buyIn.Value, NpcReserve = (long)reserve.Value,
                DiceMinimumBet = (long)minimum.Value, DiceMaximumBet = (long)maximum.Value, DiceBetStep = (long)step.Value,
                DiceOutsideReturn = (int)outside.Value, DiceSevenReturn = (int)seven.Value };
            if (!draft.HasValidSettings())
            { error.Text = T("Choose a currency, valid wager limits divisible by the step, and a reserve covering the largest possible net win."); return; }
            command.Game = draft.Game; command.TableId = draft.TableId; command.MaxPlayers = draft.MaxPlayers;
            command.CurrencyItemId = draft.CurrencyItemId; command.StartingChips = draft.StartingChips;
            command.NpcReserve = draft.NpcReserve; command.UnlimitedNpcBankroll = false;
            command.DiceMinimumBet = draft.DiceMinimumBet; command.DiceMaximumBet = draft.DiceMaximumBet;
            command.DiceBetStep = draft.DiceBetStep; command.DiceOutsideReturn = draft.DiceOutsideReturn;
            command.DiceSevenReturn = draft.DiceSevenReturn; command.DealerPlays = false; command.NpcPlayers = 0;
            form.DialogResult = DialogResult.OK; form.Close();
        };
        footer.Controls.Add(cancel); footer.Controls.Add(save); form.Controls.Add(grid); form.Controls.Add(footer);
        form.AcceptButton = save; form.CancelButton = cancel;
    }
}
