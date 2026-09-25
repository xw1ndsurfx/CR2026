using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
using Intersect.Network.Packets.MiniGames;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;

namespace Intersect.Client.Interface.Game;

internal sealed partial class PokerWindow
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
    private readonly ImagePanel?[] _portraitSkinCurrency = new ImagePanel?[6];
    private readonly ImagePanel?[] _boardSkinSlots = new ImagePanel?[5];
    private readonly ImagePanel?[] _ownSkinSlots = new ImagePanel?[2];

    private void InitializeTableSkin()
    {
        _tableSkin = CreateTableSkinImage(_content, "PokerTableSkin", "table_poker_1.png");
        _topSkin = CreateTableSkinImage(_content, "PokerTopSkin", "panel_top.png");
        _timerSkin = CreateTableSkinImage(_content, "PokerTimerSkin", "panel_timer.png");
        _timerFillSkin = CreateTableSkinImage(_content, "PokerTimerFillSkin", "panel_timer_fill.png");

        _portraitSkinButton = new Button(_content, "PokerPortraitsButton")
        {
            Font = Skin.DefaultFont,
            FontSize = 10,
            Text = string.Empty,
        };
        ApplyTableSkinButton(_portraitSkinButton, "button_portraits.png");
        _portraitSkinButton.Clicked += (_, _) =>
        {
            _backTray.IsHidden = true;
            if (_portraitTray == null) return;
            _portraitTray.IsHidden = !_portraitTray.IsHidden;
            if (!_portraitTray.IsHidden)
            {
                RefreshPortraitPicker();
                _portraitTray.BringToFront();
            }
        };

        _portraitTray = new PokerFlatPanel(_content, "PortraitPicker") { IsHidden = true };
        for (var i = 0; i < TablePortraitPreference.Count; ++i)
        {
            var id = i;
            var choice = new Button(_portraitTray, "PortraitChoice" + i)
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
            _portraitChoiceImages[i] = new ImagePanel(_portraitTray, "PortraitChoicePreview" + i)
            {
                ShouldDrawBackground = false,
                MouseInputEnabled = false,
                KeyboardInputEnabled = false,
                Texture = TablePortraitPreference.Texture(i),
            };
        }

        for (var slot = 0; slot < 6; ++slot)
        {
            _portraitSkinFrames[slot] = CreateTableSkinImage(_content, "PokerPortraitFrame" + slot, "portrait_0.png");
            _portraitSkinDetails[slot] = CreateTableSkinImage(_content, "PokerPortraitDetails" + slot, "portrait_details.png");
            _portraitSkinActions[slot] = CreateTableSkinImage(_content, "PokerPortraitAction" + slot, "portrait_action.png");
            _portraitSkinCurrency[slot] = CreateTableSkinImage(_content, "PokerPortraitCurrency" + slot, "table_currency.png");
            _portraitSkinActions[slot]!.IsHidden = true;
            _portraitSkinCurrency[slot]!.IsHidden = true;
        }

        for (var i = 0; i < 5; ++i)
        {
            _boardSkinSlots[i] = CreateTableSkinImage(_content, "PokerBoardSlot" + i, "table_empty.png");
        }

        for (var i = 0; i < 2; ++i)
        {
            _ownSkinSlots[i] = CreateTableSkinImage(_content, "PokerOwnSlot" + i, "table_empty.png");
        }

        _cardsSkinApplied = ApplyTableSkinButton(_back, "button_cards.png");
        _leaveSkinButton = _content.Children.OfType<Button>().FirstOrDefault(button => button.Name == "Leave");
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

        foreach (var image in _boardSkinSlots)
        {
            image?.SendToBack();
        }

        foreach (var image in _ownSkinSlots)
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

        SetTableSkinBounds(_tableSkin, _layout.Rect(180, 72, 640, 512));
        if (_tableSkin.Texture != null)
        {
            // The table is rendered by PokerWindow.Render so the procedural
            // semi-3D motions can be drawn on top of it.
            _tableSkin.IsHidden = true;
        }

        SetTableSkinBounds(_topSkin, _layout.Rect(319, 10, 362, 48));
        SetTableSkinBounds(_timerSkin, _layout.Rect(926, 92, 54, 113));
        SetTableSkinBounds(_timerFillSkin, _layout.Rect(945, 113, 16, 82));

        if (_portraitSkinButton != null)
        {
            var portraits = _layout.Rect(684, 656, 96, 108);
            _portraitSkinButton.SetBounds(portraits.X, portraits.Y, portraits.Width, portraits.Height);
        }

        if (_portraitTray != null)
        {
            var tray = _layout.Rect(168, 398, 664, 150);
            _portraitTray.SetBounds(tray.X, tray.Y, tray.Width, tray.Height);
            for (var i = 0; i < TablePortraitPreference.Count; ++i)
            {
                var choice = _portraitChoiceButtons[i];
                if (choice != null)
                {
                    var bounds = _layout.LocalRect(8 + i * 108, 12, 100, 126);
                    choice.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    choice.FontSize = _layout.FontSize(14);
                }

                var preview = _portraitChoiceImages[i];
                if (preview != null)
                {
                    BlackjackCardStrip.Fit(
                        preview,
                        TablePortraitPreference.Texture(i),
                        _layout.LocalRect(24 + i * 108, 23, 68, 82)
                    );
                }
            }
        }

        for (var slot = 0; slot < 6; ++slot)
        {
            var center = PokerSceneLayout.Center(slot);
            var portraitY = slot == 3 ? 72 : center.Y - 43;
            var detailsY = slot == 3 ? 112 : center.Y + 29;
            SetTableSkinBounds(_portraitSkinFrames[slot], _layout.Rect(center.X - 34, portraitY, 67, 75));
            SetTableSkinBounds(_portraitSkinDetails[slot], _layout.Rect(center.X - 104, detailsY, 208, 61));
            SetTableSkinBounds(_portraitSkinActions[slot], _layout.Rect(center.X - 37, detailsY - 26, 74, 33));
            SetTableSkinBounds(_portraitSkinCurrency[slot], _layout.Rect(center.X - 88, detailsY + 29, 18, 18));

            var name = _layout.Rect(center.X - 98, detailsY + 2, 196, 20);
            _names[slot].SetBounds(name.X, name.Y, name.Width, name.Height);
            _names[slot].FontSize = _layout.FontSize(10);
            _names[slot].TextAlign = Pos.Left | Pos.CenterV;

            var stack = _layout.Rect(center.X - 72, detailsY + 22, 170, 18);
            _stacks[slot].SetBounds(stack.X, stack.Y, stack.Width, stack.Height);
            _stacks[slot].FontSize = _layout.FontSize(9);
            _stacks[slot].TextAlign = Pos.Left | Pos.CenterV;

            var state = _layout.Rect(center.X - 98, detailsY + 40, 196, 18);
            _seatCards[slot].SetBounds(state.X, state.Y, state.Width, state.Height);
            _seatCards[slot].FontSize = _layout.FontSize(9);
            _seatCards[slot].TextAlign = Pos.Left | Pos.CenterV;

            var action = _layout.Rect(center.X - 37, detailsY - 25, 74, 30);
            _decisions[slot].SetBounds(action.X, action.Y, action.Width, action.Height);
            _decisions[slot].FontSize = _layout.FontSize(8);
            _decisions[slot].TextAlign = Pos.Center;
            _decisions[slot].BringToFront();
        }

        for (var i = 0; i < 5; ++i)
        {
            SetTableSkinBounds(_boardSkinSlots[i], _layout.Rect(337 + i * 66, 306, 49, 65));
        }

        var own = PokerSceneLayout.Cards(0);
        for (var i = 0; i < 2; ++i)
        {
            SetTableSkinBounds(_ownSkinSlots[i], _layout.Rect(own.X + i * 58 + 2, own.Y + 2, 49, 65));
        }

        if (_topSkin.Texture != null)
        {
            var title = _layout.Rect(329, 18, 342, 24);
            _table.SetBounds(title.X, title.Y, title.Width, title.Height);
            _table.TextAlign = Pos.Center;

            var turn = _layout.Rect(330, 54, 340, 28);
            _turn.SetBounds(turn.X, turn.Y, turn.Width, turn.Height);
            _turn.TextAlign = Pos.Center;
        }

        var skinnedActions = _tableSkin.Texture != null;
        foreach (var button in TableActionButtons())
        {
            button.ShouldDrawBackground = !skinnedActions;
        }

        if (skinnedActions)
        {
            var raiseLabel = _content.Children.OfType<Label>().FirstOrDefault(label => label.Name == "RaiseTotal");
            if (raiseLabel != null)
            {
                var bounds = _layout.Rect(52, 594, 136, 28);
                raiseLabel.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                raiseLabel.FontSize = _layout.FontSize(10);
            }

            var amount = _layout.Rect(200, 592, 120, 32);
            _amount.SetBounds(amount.X, amount.Y, amount.Width, amount.Height);

            var minimum = _layout.Rect(330, 592, 100, 32);
            _minimum.SetBounds(minimum.X, minimum.Y, minimum.Width, minimum.Height);

            var raise = _layout.Rect(440, 592, 140, 32);
            _raise.SetBounds(raise.X, raise.Y, raise.Width, raise.Height);

            var error = _layout.Rect(52, 558, 610, 26);
            _error.SetBounds(error.X, error.Y, error.Width, error.Height);
            _error.FontSize = _layout.FontSize(10);

            var start = _layout.Rect(52, 634, 100, 32);
            _start.SetBounds(start.X, start.Y, start.Width, start.Height);
            var fold = _layout.Rect(162, 634, 90, 32);
            _fold.SetBounds(fold.X, fold.Y, fold.Width, fold.Height);
            var check = _layout.Rect(262, 634, 90, 32);
            _check.SetBounds(check.X, check.Y, check.Width, check.Height);
            var call = _layout.Rect(362, 634, 100, 32);
            _call.SetBounds(call.X, call.Y, call.Width, call.Height);
            var allIn = _layout.Rect(472, 634, 90, 32);
            _allIn.SetBounds(allIn.X, allIn.Y, allIn.Width, allIn.Height);
            var refresh = _layout.Rect(572, 634, 90, 32);
            _refresh.SetBounds(refresh.X, refresh.Y, refresh.Width, refresh.Height);

            var experience = _layout.Rect(52, 678, 610, 28);
            _experience.SetBounds(experience.X, experience.Y, experience.Width, experience.Height);

            var footer = _content.Children.OfType<Label>().FirstOrDefault(label => label.Name == "TestOnly");
            if (footer != null)
            {
                var footerBounds = _layout.Rect(52, 742, 610, 26);
                footer.SetBounds(footerBounds.X, footerBounds.Y, footerBounds.Width, footerBounds.Height);
                footer.FontSize = _layout.FontSize(9);
            }
        }

        if (_cardsSkinApplied)
        {
            var cards = _layout.Rect(792, 656, 96, 108);
            _back.SetBounds(cards.X, cards.Y, cards.Width, cards.Height);
        }

        if (_leaveSkinApplied && _leaveSkinButton != null)
        {
            var leave = _layout.Rect(900, 656, 96, 108);
            _leaveSkinButton.SetBounds(leave.X, leave.Y, leave.Width, leave.Height);
        }

        var status = _layout.Rect(684, 742, 312, 26);
        _backStatus.SetBounds(status.X, status.Y, status.Width, status.Height);
        _backStatus.TextAlign = Pos.Center;
        _backStatus.IsHidden = _tableSkin.Texture != null;

        foreach (var feed in _feed)
        {
            feed.IsHidden = _tableSkin.Texture != null;
        }
    }

    private void UpdateTableSkin(PokerClientModel model, PokerTableState state, PokerPlayerState me)
    {
        RefreshPortraitPicker();
        var now = Environment.TickCount64;
        var playing = state.Stage is >= PokerStage.PreFlop and <= PokerStage.River;

        for (var slot = 0; slot < 6; ++slot)
        {
            var seat = state.Seats.FirstOrDefault(candidate => PokerSceneLayout.Slot(candidate.Seat, me.Seat) == slot);
            if (_portraitSkinFrames[slot] != null)
            {
                _portraitSkinFrames[slot]!.TextureFilename = seat?.PlayerId == state.DealerNpcId
                    ? "table/portrait_1.png"
                    : "table/portrait_0.png";
            }

            SetTableSkinVisible(_portraitSkinFrames[slot], seat != null);
            SetTableSkinVisible(_portraitSkinDetails[slot], seat != null);
            SetTableSkinVisible(_portraitSkinCurrency[slot], seat != null);
            SetTableSkinVisible(
                _portraitSkinActions[slot],
                seat != null && playing && seat.Seat == state.ActingSeat
            );
        }

        var seconds = playing ? Math.Max(0, model.SecondsRemaining(now)) : 0;
        UpdateTableSkinTimer(playing && state.ActingSeat >= 0 && seconds > 0, seconds);
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

    private IEnumerable<Button> TableActionButtons()
    {
        yield return _minimum;
        yield return _raise;
        yield return _start;
        yield return _fold;
        yield return _check;
        yield return _call;
        yield return _allIn;
        yield return _refresh;
    }

    private void RenderTableActionPanel(RendererBase renderer)
    {
        var panel = _layout.Rect(36, 581, 640, 187);
        renderer.DrawColor = new Color(24, 17, 14);
        renderer.DrawFilledRect(new Rectangle(panel.X, panel.Y, panel.Width, panel.Height));

        var top = _layout.Rect(36, 581, 640, 2);
        renderer.DrawColor = new Color(145, 98, 53);
        renderer.DrawFilledRect(new Rectangle(top.X, top.Y, top.Width, top.Height));

        var divider = _layout.Rect(52, 671, 610, 1);
        renderer.DrawColor = new Color(76, 52, 40);
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
