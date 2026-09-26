using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.MiniGames.Blackjack;
using Intersect.Network.Packets.MiniGames;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;

namespace Intersect.Client.Interface.Game;

internal sealed partial class BlackjackWindow
{
    private ImagePanel? _tableSkin;
    private ImagePanel? _topSkin;
    private ImagePanel? _timerSkin;
    private ImagePanel? _timerFillSkin;
    private Button? _portraitSkinButton;
    private PokerFlatPanel? _portraitTray;
    private readonly Button?[] _portraitChoiceButtons = new Button?[TablePortraitPreference.Count];
    private readonly ImagePanel?[] _portraitChoiceImages = new ImagePanel?[TablePortraitPreference.Count];
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

        _portraitSkinButton = new Button(this, "BlackjackPortraitsButton")
        {
            Font = Skin.DefaultFont,
            FontSize = 10,
            Text = string.Empty,
        };
        ApplyTableSkinButton(_portraitSkinButton, "button_portraits.png");
        _portraitSkinButton.Clicked += (_, _) =>
        {
            _tray.IsHidden = true;
            if (_portraitTray == null) return;
            _portraitTray.IsHidden = !_portraitTray.IsHidden;
            if (!_portraitTray.IsHidden)
            {
                RefreshPortraitPicker();
                _portraitTray.BringToFront();
            }
        };

        _portraitTray = new PokerFlatPanel(this, "BlackjackPortraitPicker") { IsHidden = true };
        for (var i = 0; i < TablePortraitPreference.Count; ++i)
        {
            var id = i;
            var choice = new Button(_portraitTray, "BlackjackPortraitChoice" + i)
            {
                Font = Skin.DefaultFont,
                FontSize = 14,
                Text = string.Empty,
            };
            ApplyTableSkinButton(choice, "portrait_0.png");
            choice.Clicked += (_, _) =>
            {
                TablePortraitPreference.SelectedId = id;
                RefreshPortraitPicker();
                _portraitTray.IsHidden = true;
            };
            _portraitChoiceButtons[i] = choice;
            _portraitChoiceImages[i] = new ImagePanel(_portraitTray, "BlackjackPortraitChoicePreview" + i)
            {
                ShouldDrawBackground = false,
                MouseInputEnabled = false,
                KeyboardInputEnabled = false,
                Texture = TablePortraitPreference.Texture(i),
            };
        }

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
        if (_tableSkin.Texture != null)
        {
            // Render the table in BlackjackWindow.Render so the coded
            // semi-3D card/chip motions remain visible above it.
            _tableSkin.IsHidden = true;
        }

        SetTableSkinBounds(_topSkin, _layout.Rect(319, 12, 362, 48));
        SetTableSkinBounds(_timerSkin, _layout.Rect(926, 92, 54, 113));
        SetTableSkinBounds(_timerFillSkin, _layout.Rect(945, 113, 16, 82));

        if (_portraitSkinButton != null)
        {
            var portraits = _layout.Rect(768, 656, 96, 108);
            _portraitSkinButton.SetBounds(portraits.X, portraits.Y, portraits.Width, portraits.Height);
        }

        if (_portraitTray != null)
        {
            var tray = _layout.Rect(174, 244, 652, 164);
            _portraitTray.SetBounds(tray.X, tray.Y, tray.Width, tray.Height);
            for (var i = 0; i < TablePortraitPreference.Count; ++i)
            {
                var choice = _portraitChoiceButtons[i];
                if (choice != null)
                {
                    var bounds = _layout.LocalRect(3 + i * 108, 16, 100, 136);
                    choice.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    choice.FontSize = _layout.FontSize(14);
                }

                var preview = _portraitChoiceImages[i];
                if (preview != null)
                {
                    BlackjackCardStrip.Fit(
                        preview,
                        TablePortraitPreference.Texture(i),
                        _layout.LocalRect(19 + i * 108, 29, 68, 82)
                    );
                }
            }
        }

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

        var skinnedActions = _tableSkin.Texture != null;
        foreach (var button in TableActionButtons())
        {
            button.ShouldDrawBackground = !skinnedActions;
        }

        if (skinnedActions)
        {
            var wagerLabel = Children.OfType<Label>().FirstOrDefault(label => label.Name == "BlackjackBetLabel");
            if (wagerLabel != null)
            {
                var bounds = _layout.Rect(40, 582, 100, 28);
                wagerLabel.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                wagerLabel.FontSize = _layout.FontSize(10);
            }

            var amount = _layout.Rect(150, 580, 100, 32);
            _amount.SetBounds(amount.X, amount.Y, amount.Width, amount.Height);
            var bet = _layout.Rect(260, 580, 110, 32);
            _bet.SetBounds(bet.X, bet.Y, bet.Width, bet.Height);
            var start = _layout.Rect(380, 580, 120, 32);
            _start.SetBounds(start.X, start.Y, start.Width, start.Height);

            var error = _layout.Rect(510, 580, 120, 32);
            _error.SetBounds(error.X, error.Y, error.Width, error.Height);
            _error.FontSize = _layout.FontSize(9);

            var hit = _layout.Rect(40, 626, 105, 32);
            _hit.SetBounds(hit.X, hit.Y, hit.Width, hit.Height);
            var stand = _layout.Rect(155, 626, 105, 32);
            _stand.SetBounds(stand.X, stand.Y, stand.Width, stand.Height);
            var doubleButton = _layout.Rect(270, 626, 105, 32);
            _double.SetBounds(doubleButton.X, doubleButton.Y, doubleButton.Width, doubleButton.Height);
            var split = _layout.Rect(385, 626, 105, 32);
            _split.SetBounds(split.X, split.Y, split.Width, split.Height);
            var refresh = _layout.Rect(500, 626, 120, 32);
            _refresh.SetBounds(refresh.X, refresh.Y, refresh.Width, refresh.Height);

            var xp = _layout.Rect(40, 675, 580, 28);
            _xp.SetBounds(xp.X, xp.Y, xp.Width, xp.Height);

            var notice = Children.OfType<Label>().FirstOrDefault(label => label.Name == "BlackjackNotice");
            if (notice != null)
            {
                notice.IsHidden = true;
            }
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
        RefreshPortraitPicker();
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

    private void RefreshPortraitPicker()
    {
        for (var i = 0; i < TablePortraitPreference.Count; ++i)
        {
            var button = _portraitChoiceButtons[i];
            if (button != null)
            {
                button.Text = i == TablePortraitPreference.SelectedId ? "✓" : string.Empty;
                button.TextColorOverride = i == TablePortraitPreference.SelectedId ? Gold : Color.White;
            }

            var preview = _portraitChoiceImages[i];
            if (preview != null)
            {
                preview.Texture = TablePortraitPreference.Texture(i);
            }
        }
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

    private void RenderTableSkinBackground(RendererBase renderer)
    {
        if (_tableSkin?.Texture is not { } texture)
        {
            return;
        }

        var bounds = _tableSkin.Bounds;
        renderer.DrawColor = Color.White;
        renderer.DrawTexturedRect(
            texture,
            new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            Color.White,
            0f,
            0f,
            1f,
            1f
        );
    }

    private void RenderLocalHandInfoBackground(RendererBase renderer)
    {
        if (_tableSkin?.Texture == null || _state == null)
        {
            return;
        }

        var me = _state.Seats.FirstOrDefault(seat => seat.Seat == _localSeat);
        if (me == null || me.Hands.Length == 0)
        {
            return;
        }

        var split = me.Hands.Length > 1;
        for (var hand = 0; hand < me.Hands.Length && hand < 2; ++hand)
        {
            var cardX = split ? 350 + hand * 155 : 420;
            var cardW = split ? 140 : 160;
            var infoX = split ? cardX : cardX - 15;
            var infoW = split ? cardW : cardW + 30;
            var bounds = _layout.Rect(infoX, 386, infoW, 32);

            renderer.DrawColor = new Color(63, 38, 31);
            renderer.DrawFilledRect(new Rectangle(bounds.X - 1, bounds.Y - 1, bounds.Width + 2, bounds.Height + 2));

            renderer.DrawColor = new Color(20, 24, 22);
            renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        }
    }

    private IEnumerable<Button> TableActionButtons()
    {
        yield return _bet;
        yield return _start;
        yield return _hit;
        yield return _stand;
        yield return _double;
        yield return _split;
        yield return _refresh;
    }

    private void RenderTableActionPanel(RendererBase renderer)
    {
        var panel = _layout.Rect(24, 570, 620, 198);
        renderer.DrawColor = new Color(20, 28, 24);
        renderer.DrawFilledRect(new Rectangle(panel.X, panel.Y, panel.Width, panel.Height));

        var top = _layout.Rect(24, 570, 620, 2);
        renderer.DrawColor = new Color(145, 98, 53);
        renderer.DrawFilledRect(new Rectangle(top.X, top.Y, top.Width, top.Height));

        var divider = _layout.Rect(40, 668, 580, 1);
        renderer.DrawColor = new Color(56, 77, 62);
        renderer.DrawFilledRect(new Rectangle(divider.X, divider.Y, divider.Width, divider.Height));

        foreach (var button in TableActionButtons())
        {
            DrawTableActionButton(renderer, button);
        }
    }

    private static void DrawTableActionButton(RendererBase renderer, Button button)
    {
        var outer = new Rectangle(button.X - 1, button.Y - 1, button.Width + 2, button.Height + 2);
        renderer.DrawColor = new Color(126, 84, 56);
        renderer.DrawFilledRect(outer);

        var inner = new Rectangle(button.X, button.Y, button.Width, button.Height);
        renderer.DrawColor = button.IsDisabled
            ? new Color(57, 30, 34)
            : new Color(99, 66, 53);
        renderer.DrawFilledRect(inner);
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
