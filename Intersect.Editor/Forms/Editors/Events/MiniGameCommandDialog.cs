using System.Drawing;
using System.Windows.Forms;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Configuration;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Editors.Events;

/// <summary>Edits a detached draft; Cancel never changes the command.</summary>
internal sealed class MiniGameCommandDialog : Form
{
    private sealed record AnimationChoice(Guid Id, string Name) { public override string ToString() => Name; }
    private sealed record CurrencyChoice(Guid Id, string Name) { public override string ToString() => Name; }
    private sealed record SoundChoice(string File, string Name) { public override string ToString() => Name; }
    private sealed record RewardItemChoice(Guid Id, string Name) { public override string ToString() => Name; }
    private sealed record RewardListChoice(PokerLevelReward Reward, string Name) { public override string ToString() => Name; }
    private sealed record GameChoice(MiniGameType Type, string Name) { public override string ToString() => Name; }
    public MiniGameCommandDialog(StartMiniGameCommand command)
    {
        Text = "Start Mini-Game - Poker"; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false; AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(660, Math.Min(740, Math.Max(480, (Screen.PrimaryScreen?.WorkingArea.Height ?? 900) - 140)));
        MinimumSize = new Size(580, 420);
        BackColor = DrawingColor.FromArgb(45, 45, 48); ForeColor = DrawingColor.Gainsboro;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 46, AutoScroll = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        for (var row = 0; row < 46; ++row) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 8, 12, 8) };
        Controls.Add(layout); Controls.Add(buttons);
        var hint = new Label { AutoSize = true, MaximumSize = new Size(590, 0), Margin = new Padding(3, 3, 3, 12),
            Text = "Same map instance + Table ID = shared table. Use identical settings on all access events. " +
                "Marlow deals and plays. 25 XP per positive-net human win; B1-B6 unlock at levels 1/5/10/15/20/25. " +
                "Show the buy-in amount in a confirmation event before this command." };
        layout.Controls.Add(hint, 0, 0); layout.SetColumnSpan(hint, 2);
        var game = new ComboBox { Name = "MiniGameType", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        foreach (var definition in MiniGameCatalog.All)
            game.Items.Add(new GameChoice(definition.Type, definition.DisplayName));
        game.SelectedItem = game.Items.Cast<GameChoice>().FirstOrDefault(choice => choice.Type == command.Game) ?? game.Items[0];
        var table = new TextBox { Name = "TableId", Text = command.TableId ?? "", MaxLength = 64, Dock = DockStyle.Fill };
        var seats = Number(command.MaxPlayers, 2, 6);
        var currency = CurrencyPicker(command.CurrencyItemId);
        var chips = Number(command.StartingChips, 1, 1_000_000_000); chips.Name = "StartingChips";
        var reserve = Number(command.NpcReserve, 0, 1_000_000_000); reserve.Name = "NpcReserve";
        var unlimitedNpcBankroll = new CheckBox
        {
            Name = "UnlimitedNpcBankroll", Text = "House / system-funded NPCs (unlimited)",
            Checked = command.UnlimitedNpcBankroll, AutoSize = true
        };
        var small = Number(command.SmallBlind, 1, 1_000_000_000); var big = Number(command.BigBlind, 1, 1_000_000_000);
        var seconds = Number(command.TurnSeconds, 5, 300);
        var dealer = new CheckBox { Text = "Marlow / Croupier", Checked = command.DealerPlays, AutoSize = true };
        var npcs = Number(command.NpcPlayers, 0, 5); npcs.Name = "NpcPlayers";
        var automatic = new CheckBox { Text = "Next hand after 5 seconds", Checked = command.AutoStart, AutoSize = true };
        var animation = AnimationPicker(command.DealAnimationId); var victory = AnimationPicker(command.VictoryAnimationId);
        var checkAnimation = AnimationPicker(command.CheckAnimationId); var callAnimation = AnimationPicker(command.CallAnimationId);
        var raiseAnimation = AnimationPicker(command.RaiseAnimationId); var foldAnimation = AnimationPicker(command.FoldAnimationId);
        var allInAnimation = AnimationPicker(command.AllInAnimationId); var loseAnimation = AnimationPicker(command.LoseAnimationId);
        var levelUpAnimation = AnimationPicker(command.LevelUpAnimationId); var joinAnimation = AnimationPicker(command.JoinAnimationId);
        var leaveAnimation = AnimationPicker(command.LeaveAnimationId);
        var dealSound = SoundPicker("DealSound", command.DealSound);
        var checkSound = SoundPicker("CheckSound", command.CheckSound);
        var callSound = SoundPicker("CallSound", command.CallSound);
        var raiseSound = SoundPicker("RaiseSound", command.RaiseSound);
        var foldSound = SoundPicker("FoldSound", command.FoldSound);
        var allInSound = SoundPicker("AllInSound", command.AllInSound);
        var winSound = SoundPicker("WinSound", command.WinSound);
        var loseSound = SoundPicker("LoseSound", command.LoseSound);
        var levelUpSound = SoundPicker("LevelUpSound", command.LevelUpSound);
        var joinSound = SoundPicker("JoinSound", command.JoinSound);
        var leaveSound = SoundPicker("LeaveSound", command.LeaveSound);
        var announce = new CheckBox { Text = "Human name + positive net win", Checked = command.AnnounceWins, AutoSize = true };
        var motionSpeed = new ComboBox { Name = "ProceduralAnimationSpeed", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        motionSpeed.Items.AddRange(Enum.GetNames<PokerMotionSpeed>());
        motionSpeed.SelectedItem = command.ProceduralAnimationSpeed.ToString();
        var motionPanel = new FlowLayoutPanel { Name = "ProceduralAnimationOptions", AutoSize = true, WrapContents = true, Dock = DockStyle.Fill };
        CheckBox Motion(string name, string text, bool value)
        {
            var box = new CheckBox { Name = name, Text = text, Checked = value, AutoSize = true };
            motionPanel.Controls.Add(box);
            return box;
        }
        var motionDeal = Motion("AnimateDealCards", "Deal cards", command.AnimateDealCards);
        var motionBoard = Motion("AnimateBoardCards", "Flop / Turn / River", command.AnimateBoardCards);
        var motionChips = Motion("AnimateChips", "Chips", command.AnimateChips);
        var motionShowdown = Motion("AnimateShowdown", "Showdown", command.AnimateShowdown);
        var motionShuffle = Motion("AnimateShuffle", "Shuffle / new hand", command.AnimateShuffle);
        var motionAllIn = Motion("AnimateAllIn", "All-in emphasis", command.AnimateAllIn);
        var backs = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        for (var id = 0; id < MiniGameProgression.BackCount; ++id) backs.Items.Add($"B{id + 1} (B{id + 1}.png)");
        backs.SelectedIndex = Math.Clamp(command.NpcCardBackId, 0, MiniGameProgression.BackCount - 1);
        var blackjackMinimum = Number(command.BlackjackMinimumBet, 2, 1_000_000_000); blackjackMinimum.Name = "BlackjackMinimumBet"; blackjackMinimum.Increment = 2;
        var blackjackMaximum = Number(command.BlackjackMaximumBet, 2, 1_000_000_000); blackjackMaximum.Name = "BlackjackMaximumBet"; blackjackMaximum.Increment = 2;
        var blackjackHitSoft17 = new CheckBox { Name = "BlackjackHitSoft17", Text = "Dealer hits soft 17 (H17)", Checked = command.BlackjackHitSoft17, AutoSize = true };
        bool IsBlackjack() => (game.SelectedItem as GameChoice)?.Type == MiniGameType.Blackjack;
        bool IsPotions() => (game.SelectedItem as GameChoice)?.Type == MiniGameType.Potions;
        var currencyModeFunded = command.CurrencyItemId != Guid.Empty;
        long testChipsValue = command.CurrencyItemId == Guid.Empty ? command.StartingChips : 1000;
        long fundedBuyInValue = command.CurrencyItemId != Guid.Empty ? command.StartingChips : 0;

        long SuggestedFundedBuyIn()
        {
            if (!IsBlackjack()) return Math.Clamp(testChipsValue, 1, 1_000_000_000);
            var minimum = (long)blackjackMinimum.Value;
            var maximum = (long)blackjackMaximum.Value;
            return Math.Clamp(Math.Max(maximum, minimum * 10), 1, 1_000_000_000);
        }

        long SuggestedBlackjackReserve()
        {
            var buyIn = (long)chips.Value;
            var maximumBet = (long)blackjackMaximum.Value;
            var seatCount = (long)seats.Value;
            return Math.Clamp(Math.Max(buyIn, maximumBet * 4L * seatCount), 1, 1_000_000_000);
        }

        void EnsureBlackjackReserve()
        {
            var funded = ((currency.SelectedItem as CurrencyChoice)?.Id ?? Guid.Empty) != Guid.Empty;
            if (!funded || !IsBlackjack()) return;

            var suggested = SuggestedBlackjackReserve();
            if ((long)reserve.Value < suggested)
            {
                reserve.Value = suggested;
            }
        }

        void ChangeCurrencyMode()
        {
            var funded = ((currency.SelectedItem as CurrencyChoice)?.Id ?? Guid.Empty) != Guid.Empty;
            if (funded == currencyModeFunded) return;

            if (currencyModeFunded) fundedBuyInValue = (long)chips.Value;
            else testChipsValue = (long)chips.Value;

            currencyModeFunded = funded;
            var next = funded
                ? (fundedBuyInValue > 0 ? fundedBuyInValue : SuggestedFundedBuyIn())
                : testChipsValue;
            chips.Value = Math.Clamp(next, (long)chips.Minimum, (long)chips.Maximum);
            if (funded && fundedBuyInValue == 0) fundedBuyInValue = (long)chips.Value;

            EnsureBlackjackReserve();
        }

        void LimitNpcs()
        {
            var maximum = seats.Value - 1 - (IsBlackjack() ? 1 : (dealer.Checked ? 1 : 0));
            maximum = Math.Max(0, maximum);
            if (npcs.Value > maximum) npcs.Value = maximum;
            npcs.Maximum = maximum;
        }
        void UpdateGameUi()
        {
            var blackjack = IsBlackjack();
            var potions = IsPotions();

            small.Enabled = big.Enabled = dealer.Enabled = !blackjack && !potions;
            blackjackMinimum.Enabled = blackjackMaximum.Enabled = blackjackHitSoft17.Enabled = blackjack;
            seats.Enabled = npcs.Enabled = currency.Enabled = chips.Enabled = reserve.Enabled =
                unlimitedNpcBankroll.Enabled = automatic.Enabled = !potions;
            backs.Enabled = motionSpeed.Enabled = motionPanel.Enabled = !potions;

            if (blackjack)
            {
                dealer.Checked = false;
                unlimitedNpcBankroll.Checked = false;
            }
            if (potions)
            {
                dealer.Checked = false;
                npcs.Value = 0;
                unlimitedNpcBankroll.Checked = false;
            }

            LimitNpcs();
            EnsureBlackjackReserve();
        }
        seats.ValueChanged += (_, _) => { LimitNpcs(); EnsureBlackjackReserve(); };
        dealer.CheckedChanged += (_, _) => LimitNpcs();
        LimitNpcs();
        AddRow(layout, 1, "Mini-game", game); AddRow(layout, 2, "Table ID (letters, digits, - or _)", table);
        AddRow(layout, 3, "Maximum seats (humans + NPCs)", seats); AddRow(layout, 4, "Table currency / Monnaie", currency);
        var chipsLabel = AddRow(layout, 5, "Starting test chips", chips);
        AddRow(layout, 6, "Small blind", small); AddRow(layout, 7, "Big blind", big); AddRow(layout, 8, "Turn timeout (seconds)", seconds);
        AddRow(layout, 9, "Dealer plays and deals", dealer); AddRow(layout, 10, "Other NPC opponents", npcs);
        AddRow(layout, 11, "Automatic hands", automatic); AddRow(layout, 12, "Dealing animation", animation);
        AddRow(layout, 13, "Announce wins in GLOBAL chat", announce); AddRow(layout, 14, "Victory animation (winner only)", victory);
        AddRow(layout, 15, "Dealer / NPC card back", backs); AddRow(layout, 16, "Dealer / NPC bank reserve", reserve);
        AddRow(layout, 17, "Unlimited NPC bankroll", unlimitedNpcBankroll);
        AddRow(layout, 18, "Sound - Deal", dealSound); AddRow(layout, 19, "Sound - Check", checkSound);
        AddRow(layout, 20, "Sound - Call", callSound); AddRow(layout, 21, "Sound - Raise", raiseSound);
        AddRow(layout, 22, "Sound - Fold", foldSound); AddRow(layout, 23, "Sound - All-in", allInSound);
        AddRow(layout, 24, "Sound - Win", winSound); AddRow(layout, 25, "Sound - Lose", loseSound);
        AddRow(layout, 26, "Sound - Level Up", levelUpSound); AddRow(layout, 27, "Sound - Join table", joinSound);
        AddRow(layout, 28, "Sound - Leave table", leaveSound);
        AddRow(layout, 29, "Animation - Check", checkAnimation); AddRow(layout, 30, "Animation - Call", callAnimation);
        AddRow(layout, 31, "Animation - Raise", raiseAnimation); AddRow(layout, 32, "Animation - Fold", foldAnimation);
        AddRow(layout, 33, "Animation - All-in", allInAnimation); AddRow(layout, 34, "Animation - Lose", loseAnimation);
        AddRow(layout, 35, "Animation - Level Up", levelUpAnimation); AddRow(layout, 36, "Animation - Join table", joinAnimation);
        AddRow(layout, 37, "Animation - Leave table", leaveAnimation);
        var status = new Label { Name = "CurrencyStatus", AutoSize = true, MaximumSize = new Size(590, 0), Margin = new Padding(3, 12, 3, 12) };
        layout.Controls.Add(status, 0, 38); layout.SetColumnSpan(status, 2);
        var summary = new Label { Name = "ConfigurationSummary", AutoSize = true, MaximumSize = new Size(590, 0),
            Margin = new Padding(3, 3, 3, 12), ForeColor = DrawingColor.LightSkyBlue };
        layout.Controls.Add(summary, 0, 39); layout.SetColumnSpan(summary, 2);

        var rewardDraft = (command.LevelRewards ?? []).ToList();
        var rewardList = new ListBox { Name = "LevelRewards", Width = 360, Height = 110 };
        var rewardLevel = new ComboBox { Name = "RewardLevel", DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
        for (var level = 2; level <= MiniGameProgression.MaximumLevel; ++level) rewardLevel.Items.Add(level);
        rewardLevel.SelectedIndex = 0;
        var rewardItem = RewardItemPicker(Guid.Empty);
        rewardItem.Name = "RewardItem"; rewardItem.Width = 270;
        var rewardQuantity = Number(1, 1, 1_000_000_000); rewardQuantity.Name = "RewardQuantity"; rewardQuantity.Width = 100;
        void RefreshRewards()
        {
            rewardList.Items.Clear();
            foreach (var reward in rewardDraft.OrderBy(r => r.Level).ThenBy(r => ItemDescriptor.GetName(r.ItemId)))
            {
                var name = ItemDescriptor.Get(reward.ItemId)?.Name ?? ("Missing item " + reward.ItemId);
                rewardList.Items.Add(new RewardListChoice(reward, $"Level {reward.Level}: {reward.Quantity:N0} x {name}"));
            }
        }
        var addReward = new Button { Name = "AddLevelReward", Text = "Add reward", AutoSize = true };
        addReward.Click += (_, _) =>
        {
            var itemId = (rewardItem.SelectedItem as RewardItemChoice)?.Id ?? Guid.Empty;
            if (itemId == Guid.Empty) return;
            var reward = new PokerLevelReward((int)rewardLevel.SelectedItem!, itemId, (int)rewardQuantity.Value);
            if (reward.IsValid && !rewardDraft.Contains(reward)) rewardDraft.Add(reward);
            RefreshRewards();
        };
        var removeReward = new Button { Name = "RemoveLevelReward", Text = "Remove selected", AutoSize = true };
        removeReward.Click += (_, _) =>
        {
            if (rewardList.SelectedItem is RewardListChoice choice) rewardDraft.Remove(choice.Reward);
            RefreshRewards();
        };
        var rewardControls = new FlowLayoutPanel { AutoSize = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight };
        rewardControls.Controls.Add(new Label { Text = "Level", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        rewardControls.Controls.Add(rewardLevel); rewardControls.Controls.Add(rewardItem); rewardControls.Controls.Add(rewardQuantity);
        rewardControls.Controls.Add(addReward); rewardControls.Controls.Add(removeReward);
        var rewardPanel = new FlowLayoutPanel { Name = "LevelRewardPanel", AutoSize = true, WrapContents = false,
            FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill };
        rewardPanel.Controls.Add(rewardList); rewardPanel.Controls.Add(rewardControls); RefreshRewards();
        AddRow(layout, 40, "Procedural animation speed", motionSpeed);
        AddRow(layout, 41, "Procedural animation effects", motionPanel);
        AddRow(layout, 42, "Level rewards", new Label
        {
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Text = "Configured globally in Content Editors > Daily & Level Rewards Editor. All tables use the same rewards."
        });
        AddRow(layout, 43, "Blackjack minimum bet (even)", blackjackMinimum);
        AddRow(layout, 44, "Blackjack maximum bet (even)", blackjackMaximum);
        AddRow(layout, 45, "Blackjack dealer rule", blackjackHitSoft17);

        void ShowSummary()
        {
            var selectedGame = (game.SelectedItem as GameChoice)?.Type ?? MiniGameType.Poker;
            var definition = MiniGameCatalog.Get(selectedGame);
            var blackjack = selectedGame == MiniGameType.Blackjack;
            var potions = selectedGame == MiniGameType.Potions;
            var dealerSeats = blackjack ? 1 : (dealer.Checked ? 1 : 0);
            var humanSeats = Math.Max(1, (int)seats.Value - (int)npcs.Value - dealerSeats);
            var funded = ((currency.SelectedItem as CurrencyChoice)?.Id ?? Guid.Empty) != Guid.Empty;
            var tableValid = MiniGameCatalog.IsValidTableId(table.Text);

            if (potions)
            {
                summary.ForeColor = DrawingColor.LightSkyBlue;
                summary.Text = $"{definition.DisplayName} | Solo 8x10 merge board | " +
                    "Recipes, output items, required levels and XP are configured globally in Content Editors > Daily & Level Rewards Editor > Potion Recipes.";
                return;
            }

            var rules = blackjack
                ? $"Bet {blackjackMinimum.Value}-{blackjackMaximum.Value} | {(blackjackHitSoft17.Checked ? "H17" : "S17")}"
                : $"Blinds {small.Value}/{big.Value}";
            var funding = funded ? $"FUNDED buy-in {chips.Value:N0}" : $"TEST chips {chips.Value:N0}";
            summary.ForeColor = tableValid ? DrawingColor.LightSkyBlue : DrawingColor.OrangeRed;
            summary.Text = $"{definition.DisplayName} | Table: {(string.IsNullOrWhiteSpace(table.Text) ? "(missing)" : table.Text)} | " +
                $"{seats.Value} seats ({humanSeats} human available, {npcs.Value + dealerSeats} NPC/dealer) | " +
                $"{rules} | {funding} | " +
                $"{(automatic.Checked ? "Auto rounds" : "Manual start")}" +
                (tableValid ? "" : " | INVALID TABLE ID");
        }

        void ShowCurrencyStatus()
        {
            var id = (currency.SelectedItem as CurrencyChoice)?.Id ?? Guid.Empty;
            var blackjack = IsBlackjack();
            if (IsPotions())
            {
                chipsLabel.Text = "Not used by Potions";
                status.ForeColor = DrawingColor.Gold;
                status.Text = "Royal Alchemy rewards are real Intersect items configured globally in the Potion Recipes tab. " +
                    "The server validates every move, grants recipe XP, and delivers the configured output item.";
                return;
            }
            EnsureBlackjackReserve();
            reserve.Enabled = id != Guid.Empty && !unlimitedNpcBankroll.Checked;
            unlimitedNpcBankroll.Enabled = id != Guid.Empty && !blackjack;
            if (id == Guid.Empty)
            {
                chipsLabel.Text = "Starting test chips"; status.ForeColor = DrawingColor.Gainsboro;
                status.Text = "TEST mode: no inventory items are taken or paid. Test XP stays in resources/minigames-test.db.";
                return;
            }

            chipsLabel.Text = "Buy-in (inventory item units)";
            var item = ItemDescriptor.Get(id);
            if (!MiniGameCurrency.IsCompatible(item))
            {
                status.ForeColor = DrawingColor.OrangeRed;
                status.Text = "The selected item is missing or is no longer a compatible stackable item. Choose another item or test chips.";
                return;
            }

            var itemName = ItemDescriptor.GetName(id);
            status.ForeColor = DrawingColor.Gold;
            status.Text = $"Selected item: {itemName}. ID: {id}.\n" +
                $"FUNDED mode: entering the table removes exactly {chips.Value:N0} {itemName} from the player's main inventory as the buy-in. " +
                "The remaining balance is returned after leaving and settling the hand. Full inventory refunds wait safely. " +
                (blackjack
                    ? "Blackjack uses a finite house/dealer reserve. "
                    : unlimitedNpcBankroll.Checked
                        ? "UNLIMITED NPC BANKROLL is enabled: the server creates only the missing NPC buy-in when the house cannot fund a seat. "
                        : "NPC reserve creates an authorized house budget once per map + Table ID + currency. ") +
                "Funded XP is separate from test XP.";

            if (blackjack)
            {
                var suggestedReserve = SuggestedBlackjackReserve();
                status.Text += $"\nBlackjack dealer bank: {reserve.Value:N0} {itemName}. " +
                    $"The editor keeps this at or above {suggestedReserve:N0} so the dealer can cover the configured table.";
            }
        }
        currency.SelectedIndexChanged += (_, _) => { ChangeCurrencyMode(); ShowCurrencyStatus(); ShowSummary(); };
        chips.ValueChanged += (_, _) =>
        {
            if (currencyModeFunded) fundedBuyInValue = (long)chips.Value;
            else testChipsValue = (long)chips.Value;
            ShowCurrencyStatus(); ShowSummary();
        };
        reserve.ValueChanged += (_, _) => ShowCurrencyStatus();
        unlimitedNpcBankroll.CheckedChanged += (_, _) => ShowCurrencyStatus();
        game.SelectedIndexChanged += (_, _) => { UpdateGameUi(); ShowCurrencyStatus(); ShowSummary(); };
        table.TextChanged += (_, _) => ShowSummary();
        seats.ValueChanged += (_, _) => ShowSummary();
        npcs.ValueChanged += (_, _) => ShowSummary();
        dealer.CheckedChanged += (_, _) => ShowSummary();
        small.ValueChanged += (_, _) => ShowSummary();
        big.ValueChanged += (_, _) => ShowSummary();
        automatic.CheckedChanged += (_, _) => ShowSummary();
        blackjackMinimum.ValueChanged += (_, _) => { EnsureBlackjackReserve(); ShowCurrencyStatus(); ShowSummary(); };
        blackjackMaximum.ValueChanged += (_, _) => { EnsureBlackjackReserve(); ShowCurrencyStatus(); ShowSummary(); };
        blackjackHitSoft17.CheckedChanged += (_, _) => ShowSummary();
        UpdateGameUi();
        ShowCurrencyStatus();
        ShowSummary();
        var cancel = new Button { Name = "Cancel", Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var save = new Button { Name = "Save", Text = "Save", AutoSize = true };
        buttons.Controls.Add(cancel); buttons.Controls.Add(save); AcceptButton = save; CancelButton = cancel;
        save.Click += (_, _) =>
        {
            EnsureBlackjackReserve();
            var selected = IsPotions() ? Guid.Empty : (currency.SelectedItem as CurrencyChoice)?.Id ?? Guid.Empty;
            if (!IsPotions() && selected != Guid.Empty && !MiniGameCurrency.IsCompatible(ItemDescriptor.Get(selected)))
            {
                MessageBox.Show(this, "The selected item is missing or incompatible. Select a Currency or another stackable item.",
                    "Invalid table currency", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }
            var draft = new StartMiniGameCommand
            {
                Game = ((GameChoice)game.SelectedItem!).Type, TableId = table.Text,
                MaxPlayers = IsPotions() ? 1 : (int)seats.Value, CurrencyItemId = selected,
                StartingChips = (long)chips.Value, NpcReserve = (long)reserve.Value,
                UnlimitedNpcBankroll = unlimitedNpcBankroll.Checked,
                BlackjackMinimumBet = (long)blackjackMinimum.Value, BlackjackMaximumBet = (long)blackjackMaximum.Value,
                BlackjackHitSoft17 = blackjackHitSoft17.Checked,
                SmallBlind = (long)small.Value, BigBlind = (long)big.Value, TurnSeconds = (int)seconds.Value,
                DealerPlays = IsPotions() ? false : dealer.Checked,
                NpcPlayers = IsPotions() ? 0 : (int)npcs.Value,
                AutoStart = IsPotions() ? false : automatic.Checked,
                DealAnimationId = ((AnimationChoice)animation.SelectedItem!).Id, AnnounceWins = announce.Checked,
                VictoryAnimationId = ((AnimationChoice)victory.SelectedItem!).Id, NpcCardBackId = backs.SelectedIndex,
                DealSound = ((SoundChoice)dealSound.SelectedItem!).File, CheckSound = ((SoundChoice)checkSound.SelectedItem!).File,
                CallSound = ((SoundChoice)callSound.SelectedItem!).File, RaiseSound = ((SoundChoice)raiseSound.SelectedItem!).File,
                FoldSound = ((SoundChoice)foldSound.SelectedItem!).File, AllInSound = ((SoundChoice)allInSound.SelectedItem!).File,
                WinSound = ((SoundChoice)winSound.SelectedItem!).File, LoseSound = ((SoundChoice)loseSound.SelectedItem!).File,
                LevelUpSound = ((SoundChoice)levelUpSound.SelectedItem!).File, JoinSound = ((SoundChoice)joinSound.SelectedItem!).File,
                LeaveSound = ((SoundChoice)leaveSound.SelectedItem!).File,
                CheckAnimationId = ((AnimationChoice)checkAnimation.SelectedItem!).Id,
                CallAnimationId = ((AnimationChoice)callAnimation.SelectedItem!).Id,
                RaiseAnimationId = ((AnimationChoice)raiseAnimation.SelectedItem!).Id,
                FoldAnimationId = ((AnimationChoice)foldAnimation.SelectedItem!).Id,
                AllInAnimationId = ((AnimationChoice)allInAnimation.SelectedItem!).Id,
                LoseAnimationId = ((AnimationChoice)loseAnimation.SelectedItem!).Id,
                LevelUpAnimationId = ((AnimationChoice)levelUpAnimation.SelectedItem!).Id,
                JoinAnimationId = ((AnimationChoice)joinAnimation.SelectedItem!).Id,
                LeaveAnimationId = ((AnimationChoice)leaveAnimation.SelectedItem!).Id,
                ProceduralAnimationSpeed = Enum.TryParse<PokerMotionSpeed>(motionSpeed.SelectedItem?.ToString(), out var parsedMotionSpeed)
                    ? parsedMotionSpeed : PokerMotionSpeed.Normal,
                AnimateDealCards = motionDeal.Checked, AnimateBoardCards = motionBoard.Checked, AnimateChips = motionChips.Checked,
                AnimateShowdown = motionShowdown.Checked, AnimateShuffle = motionShuffle.Checked, AnimateAllIn = motionAllIn.Checked,
                LevelRewards = rewardDraft.ToArray(),
            };
            if (!draft.HasValidSettings())
            {
                MessageBox.Show(this,
                    "Check the highlighted summary. Use a valid Table ID, seat count and rules; Poker needs valid blinds and Blackjack needs even min/max bets. At least one human seat must remain.",
                    "Invalid mini-game configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }
            command.Game = draft.Game; command.TableId = draft.TableId; command.MaxPlayers = draft.MaxPlayers;
            command.CurrencyItemId = draft.CurrencyItemId; command.NpcReserve = draft.NpcReserve;
            command.UnlimitedNpcBankroll = draft.UnlimitedNpcBankroll;
            command.BlackjackMinimumBet = draft.BlackjackMinimumBet; command.BlackjackMaximumBet = draft.BlackjackMaximumBet;
            command.BlackjackHitSoft17 = draft.BlackjackHitSoft17;
            command.StartingChips = draft.StartingChips; command.SmallBlind = draft.SmallBlind; command.BigBlind = draft.BigBlind;
            command.TurnSeconds = draft.TurnSeconds; command.DealerPlays = draft.DealerPlays; command.NpcPlayers = draft.NpcPlayers;
            command.AutoStart = draft.AutoStart; command.DealAnimationId = draft.DealAnimationId; command.AnnounceWins = draft.AnnounceWins;
            command.VictoryAnimationId = draft.VictoryAnimationId; command.NpcCardBackId = draft.NpcCardBackId;
            command.DealSound = draft.DealSound; command.CheckSound = draft.CheckSound; command.CallSound = draft.CallSound;
            command.RaiseSound = draft.RaiseSound; command.FoldSound = draft.FoldSound; command.AllInSound = draft.AllInSound;
            command.WinSound = draft.WinSound; command.LoseSound = draft.LoseSound; command.LevelUpSound = draft.LevelUpSound;
            command.JoinSound = draft.JoinSound; command.LeaveSound = draft.LeaveSound;
            command.CheckAnimationId = draft.CheckAnimationId; command.CallAnimationId = draft.CallAnimationId;
            command.RaiseAnimationId = draft.RaiseAnimationId; command.FoldAnimationId = draft.FoldAnimationId;
            command.AllInAnimationId = draft.AllInAnimationId; command.LoseAnimationId = draft.LoseAnimationId;
            command.LevelUpAnimationId = draft.LevelUpAnimationId; command.JoinAnimationId = draft.JoinAnimationId;
            command.LeaveAnimationId = draft.LeaveAnimationId;
            command.ProceduralAnimationSpeed = draft.ProceduralAnimationSpeed;
            command.AnimateDealCards = draft.AnimateDealCards; command.AnimateBoardCards = draft.AnimateBoardCards;
            command.AnimateChips = draft.AnimateChips; command.AnimateShowdown = draft.AnimateShowdown;
            command.AnimateShuffle = draft.AnimateShuffle; command.AnimateAllIn = draft.AnimateAllIn;
            // Preserve legacy data for backwards compatibility; runtime rewards are global.
            command.LevelRewards = draft.LevelRewards.ToArray();
            DialogResult = DialogResult.OK; Close();
        };
    }
    private static ComboBox CurrencyPicker(Guid id)
    {
        var picker = new ComboBox { Name = "TableCurrency", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill,
            DropDownWidth = 590, AutoCompleteSource = AutoCompleteSource.ListItems, AutoCompleteMode = AutoCompleteMode.SuggestAppend };
        picker.Items.Add(new CurrencyChoice(Guid.Empty, "None / Test chips (no inventory)"));
        foreach (var item in MiniGameCurrency.CompatibleItems(ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()))
            picker.Items.Add(new CurrencyChoice(item.Id, MiniGameCurrency.DisplayName(item)));
        var selected = picker.Items.Cast<CurrencyChoice>().FirstOrDefault(choice => choice.Id == id);
        if (selected == null) { selected = new CurrencyChoice(id, "Missing / incompatible item: " + id); picker.Items.Add(selected); }
        picker.SelectedItem = selected; return picker;
    }
    private static ComboBox RewardItemPicker(Guid id)
    {
        var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 420 };
        picker.Items.Add(new RewardItemChoice(Guid.Empty, "Choose reward item..."));
        foreach (var item in ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
            picker.Items.Add(new RewardItemChoice(item.Id, string.IsNullOrWhiteSpace(item.Folder) ? item.Name : $"[{item.Folder}] / {item.Name}"));
        var selected = picker.Items.Cast<RewardItemChoice>().FirstOrDefault(choice => choice.Id == id) ?? (RewardItemChoice)picker.Items[0]!;
        picker.SelectedItem = selected;
        return picker;
    }

    private static ComboBox SoundPicker(string name, string? file)
    {
        file ??= "";
        var picker = new ComboBox { Name = name, DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 420 };
        picker.Items.Add(new SoundChoice("", "None / Aucun"));
        var soundDirectory = Path.Combine("resources", "sounds");
        var sounds = Directory.Exists(soundDirectory)
            ? Directory.GetFiles(soundDirectory, "*.wav").Select(Path.GetFileName).Where(s => !string.IsNullOrEmpty(s)).Cast<string>()
            : Array.Empty<string>();
        foreach (var sound in sounds.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            picker.Items.Add(new SoundChoice(sound, sound));
        var selected = picker.Items.Cast<SoundChoice>().FirstOrDefault(s => string.Equals(s.File, file, StringComparison.OrdinalIgnoreCase));
        if (selected == null)
        {
            selected = new SoundChoice(file, "Missing sound: " + file);
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
        layout.Controls.Add(label, 0, row); input.Margin = new Padding(3, 6, 3, 6); layout.Controls.Add(input, 1, row); return label;
    }
}
