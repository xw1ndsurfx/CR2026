using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.MiniGames.Blackjack;
using Intersect.Network.Packets.MiniGames;

namespace Intersect.Client.Interface.Game;

internal sealed partial class BlackjackWindow
{
    private ImagePanel? _tableSkin;
    private ImagePanel? _topSkin;
    private ImagePanel? _timerSkin;
    private ImagePanel? _timerFillSkin;
    private ImagePanel? _portraitsSkinButton;
    private Button? _leaveSkinButton;
    private bool _cardsSkinApplied;
    private bool _leaveSkinApplied;

    private readonly ImagePanel?[] _portraitSkinFrames = new ImagePanel?[6];
    private readonly ImagePanel?[] _portraitSkinDetails = new ImagePanel?[6];
    private readonly ImagePanel?[] _portraitSkinActions = new ImagePanel?[6];
    private readonly ImagePanel?[] _portraitSkinCurrency = new ImagePanel?[5];

    private void InitializeTableSkin()
    {
        _tableSkin = CreateTableSkinImage(this, "BlackjackTableSkin", "table_blackjack_1.png");
        _topSkin = CreateTableSkinImage(this, "BlackjackTopSkin", "panel_top.png");
        _timerSkin = CreateTableSkinImage(this, "BlackjackTimerSkin", "panel_timer.png");
        _timerFillSkin = CreateTableSkinImage(this, "BlackjackTimerFillSkin", "panel_timer_fill.png");
        _portraitsSkinButton = CreateTableSkinImage(this, "BlackjackPortraitsSkinButton", "button_portraits.png");

        for (var slot = 0; slot < 6; ++slot)
        {
            _portraitSkinFrames[slot] = CreateTableSkinImage(this, "BlackjackPortraitFrame" + slot, "portrait_0.png");
            _portraitSkinDetails[slot] = CreateTableSkinImage(this, "BlackjackPortraitDetails" + slot, "portrait_details.png");
            _portraitSkinActions[slot] = CreateTableSkinImage(this, "BlackjackPortraitAction" + slot, "portrait_action.png");
            _portraitSkinActions[slot]!.IsHidden = true;
        }

        for (var slot = 0; slot < 5; ++slot)
        {
            _portraitSkinCurrency[slot] = CreateTableSkinImage(this, "BlackjackPortraitCurrency" + slot, "table_currency.png");
            _portraitSkinCurrency[slot]!.IsHidden = true;
        }

        _cardsSkinApplied = ApplyTableSkinButton(_backToggle, "button_cards.png");
        _leaveSkinButton = Children.OfType<Button>().FirstOrDefault(button => button.Name == "BlackjackLeave");
        _leaveSkinApplied = _leaveSkinButton != null && ApplyTableSkinButton(_leaveSkinButton, "button_exit.png");

        foreach (var image in _portraitSkinDetails)
        {
            image?.SendToBack();
        }

        foreach (var image in _portraitSkinFrames)
        {
            image?.SendToBack();
        }

        foreach (var image in _portraitSkinActions)
        {
            image?.SendToBack();
        }

        _topSkin.SendToBack();
        _tableSkin.SendToBack();
    }

    private void LayoutTableSkin()
    {
        if (_tableSkin == null)
        {
            return;
        }

        SetTableSkinBounds(_tableSkin, _layout.Rect(120, 102, 760, 380));
        SetTableSkinBounds(_topSkin, _layout.Rect(319, 12, 362, 48));
        SetTableSkinBounds(_timerSkin, _layout.Rect(926, 92, 54, 113));
        SetTableSkinBounds(_timerFillSkin, _layout.Rect(945, 113, 16, 82));
        SetTableSkinBounds(_portraitsSkinButton, _layout.Rect(768, 656, 96, 108));

        for (var slot = 0; slot < 5; ++slot)
        {
            var (x, y, w) = Panel(slot);
            var frameX = slot == 0 ? 360 : x - 3;
            var frameY = slot == 0 ? 400 : y - 3;
            var detailsX = slot == 0 ? 431 : x + 39;
            var detailsY = slot == 0 ? 405 : y - 2;
            var detailsW = 208;
            var actionX = slot == 0 ? 356 : x - 8;
            var actionY = slot == 0 ? 374 : y - 29;

            SetTableSkinBounds(_portraitSkinFrames[slot], _layout.Rect(frameX, frameY, 67, 75));
            SetTableSkinBounds(_portraitSkinDetails[slot], _layout.Rect(detailsX, detailsY, detailsW, 61));
            SetTableSkinBounds(_portraitSkinActions[slot], _layout.Rect(actionX, actionY, 74, 33));
            SetTableSkinBounds(_portraitSkinCurrency[slot], _layout.Rect(detailsX + 12, detailsY + 29, 18, 18));

            var name = _layout.Rect(detailsX + 6, detailsY + 2, detailsW - 12, 20);
            _names[slot].SetBounds(name.X, name.Y, name.Width, name.Height);
            _names[slot].FontSize = _layout.FontSize(10);
            _names[slot].TextAlign = Pos.Left | Pos.CenterV;

            var balance = _layout.Rect(detailsX + 34, detailsY + 22, detailsW - 40, 20);
            _balances[slot].SetBounds(balance.X, balance.Y, balance.Width, balance.Height);
            _balances[slot].FontSize = _layout.FontSize(9);
            _balances[slot].TextAlign = Pos.Left | Pos.CenterV;

            var action = _layout.Rect(actionX, actionY + 1, 74, 30);
            _decisions[slot].SetBounds(action.X, action.Y, action.Width, action.Height);
            _decisions[slot].FontSize = _layout.FontSize(8);
            _decisions[slot].TextAlign = Pos.Center;
            _decisions[slot].BringToFront();
        }

        SetTableSkinBounds(_portraitSkinFrames[5], _layout.Rect(385, 103, 67, 75));
        SetTableSkinBounds(_portraitSkinDetails[5], _layout.Rect(456, 108, 208, 61));
        SetTableSkinBounds(_portraitSkinActions[5], _layout.Rect(382, 82, 74, 33));
        if (_portraitSkinFrames[5] != null)
        {
            _portraitSkinFrames[5]!.TextureFilename = "table/portrait_1.png";
        }

        if (_topSkin.Texture != null)
        {
            var title = _layout.Rect(324, 19, 352, 24);
            _title.SetBounds(title.X, title.Y, title.Width, title.Height);
            _title.FontSize = _layout.FontSize(16);
            _title.TextAlign = Pos.Center;

            var status = _layout.Rect(300, 58, 400, 28);
            _status.SetBounds(status.X, status.Y, status.Width, status.Height);
            _status.TextAlign = Pos.Center;
        }

        if (_cardsSkinApplied)
        {
            var cards = _layout.Rect(660, 656, 96, 108);
            _backToggle.SetBounds(cards.X, cards.Y, cards.Width, cards.Height);
        }

        if (_leaveSkinApplied && _leaveSkinButton != null)
        {
            var leave = _layout.Rect(876, 656, 96, 108);
            _leaveSkinButton.SetBounds(leave.X, leave.Y, leave.Width, leave.Height);
        }

        var backStatus = _layout.Rect(620, 742, 352, 26);
        _backStatus.SetBounds(backStatus.X, backStatus.Y, backStatus.Width, backStatus.Height);
        _backStatus.TextAlign = Pos.Center;
        _backStatus.IsHidden = _tableSkin.Texture != null;

        var dealerLabel = Children.OfType<Label>().FirstOrDefault(label => label.Name == "BlackjackDealer");
        if (dealerLabel != null)
        {
            var dealerTitle = _layout.Rect(465, 111, 190, 22);
            dealerLabel.SetBounds(dealerTitle.X, dealerTitle.Y, dealerTitle.Width, dealerTitle.Height);
            dealerLabel.FontSize = _layout.FontSize(13);
        }

        var bank = _layout.Rect(465, 133, 190, 20);
        _bank.SetBounds(bank.X, bank.Y, bank.Width, bank.Height);
        _bank.FontSize = _layout.FontSize(9);

        var dealerTotal = _layout.Rect(420, 241, 320, 24);
        _dealerTotal.SetBounds(dealerTotal.X, dealerTotal.Y, dealerTotal.Width, dealerTotal.Height);
        _dealerTotal.TextAlign = Pos.Center;

        var rules = _layout.Rect(320, 271, 360, 72);
        _rules.SetBounds(rules.X, rules.Y, rules.Width, rules.Height);
        _rules.TextAlign = Pos.Center;
    }

    private void UpdateTableSkin(BlackjackClientModel model, BlackjackTableState state, BlackjackPlayerState me)
    {
        var now = Environment.TickCount64;
        var timed = state.Stage is BlackjackStage.Betting or BlackjackStage.Players;

        for (var slot = 0; slot < 5; ++slot)
        {
            var seat = state.Seats.FirstOrDefault(candidate => (candidate.Seat - me.Seat + 5) % 5 == slot);
            SetTableSkinVisible(_portraitSkinFrames[slot], seat != null);
            SetTableSkinVisible(_portraitSkinDetails[slot], seat != null);
            SetTableSkinVisible(_portraitSkinCurrency[slot], seat != null);
            SetTableSkinVisible(
                _portraitSkinActions[slot],
                seat != null && (!string.IsNullOrWhiteSpace(seat.LastAction) ||
                    state.Stage == BlackjackStage.Players && seat.Seat == state.ActingSeat)
            );
        }

        var dealerVisible = state.DealerCards.Length > 0 || state.Stage is BlackjackStage.Players or BlackjackStage.Dealer;
        SetTableSkinVisible(_portraitSkinFrames[5], dealerVisible);
        SetTableSkinVisible(_portraitSkinDetails[5], dealerVisible);
        SetTableSkinVisible(_portraitSkinActions[5], state.Stage == BlackjackStage.Dealer);

        var seconds = timed ? Math.Max(0, model.Seconds(now)) : 0;
        UpdateTableSkinTimer(timed && seconds > 0, seconds);
    }

    private void UpdateTableSkinTimer(bool visible, long seconds)
    {
        SetTableSkinVisible(_timerSkin, visible);

        if (_timerFillSkin?.Texture is not { } texture || !visible)
        {
            if (_timerFillSkin != null)
            {
                _timerFillSkin.IsHidden = true;
            }

            return;
        }

        var fraction = Math.Clamp(seconds / 30f, 0.03f, 1f);
        var sourceHeight = Math.Max(1, (int)Math.Round(texture.Height * fraction));
        _timerFillSkin.ResetUVs();
        _timerFillSkin.SetTextureRect(0, texture.Height - sourceHeight, texture.Width, sourceHeight);

        var full = _layout.Rect(945, 113, 16, 82);
        var height = Math.Max(1, (int)Math.Round(full.Height * fraction));
        _timerFillSkin.SetBounds(full.X, full.Y + full.Height - height, full.Width, height);
        _timerFillSkin.IsHidden = false;
    }

    private static ImagePanel CreateTableSkinImage(Base parent, string name, string file)
    {
        return new ImagePanel(parent, name)
        {
            TextureFilename = "table/" + file,
            ShouldDrawBackground = false,
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };
    }

    private static bool ApplyTableSkinButton(Button button, string file)
    {
        var texture = GameContentManager.Current.GetTexture(TextureType.Gui, "table/" + file);
        if (texture == null)
        {
            return false;
        }

        button.SetAllStatesTexture(texture);
        button.Text = string.Empty;
        return true;
    }

    private static void SetTableSkinBounds(ImagePanel? image, PokerSceneRect bounds)
    {
        if (image == null)
        {
            return;
        }

        image.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        image.IsHidden = image.Texture == null;
    }

    private static void SetTableSkinVisible(ImagePanel? image, bool visible)
    {
        if (image == null)
        {
            return;
        }

        image.IsHidden = !visible || image.Texture == null;
    }
}
