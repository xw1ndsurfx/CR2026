using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
using Intersect.Network.Packets.MiniGames;

namespace Intersect.Client.Interface.Game;

internal sealed partial class PokerWindow
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
    private readonly ImagePanel?[] _portraitSkinCurrency = new ImagePanel?[6];
    private readonly ImagePanel?[] _boardSkinSlots = new ImagePanel?[5];
    private readonly ImagePanel?[] _ownSkinSlots = new ImagePanel?[2];

    private void InitializeTableSkin()
    {
        _tableSkin = CreateTableSkinImage(_content, "PokerTableSkin", "table_poker_1.png");
        _topSkin = CreateTableSkinImage(_content, "PokerTopSkin", "panel_top.png");
        _timerSkin = CreateTableSkinImage(_content, "PokerTimerSkin", "panel_timer.png");
        _timerFillSkin = CreateTableSkinImage(_content, "PokerTimerFillSkin", "panel_timer_fill.png");
        _portraitsSkinButton = CreateTableSkinImage(_content, "PokerPortraitsSkinButton", "button_portraits.png");

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
        SetTableSkinBounds(_topSkin, _layout.Rect(319, 10, 362, 48));
        SetTableSkinBounds(_timerSkin, _layout.Rect(926, 92, 54, 113));
        SetTableSkinBounds(_timerFillSkin, _layout.Rect(945, 113, 16, 82));
        SetTableSkinBounds(_portraitsSkinButton, _layout.Rect(636, 656, 96, 108));

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

        if (_cardsSkinApplied)
        {
            var cards = _layout.Rect(744, 656, 96, 108);
            _back.SetBounds(cards.X, cards.Y, cards.Width, cards.Height);
        }

        if (_leaveSkinApplied && _leaveSkinButton != null)
        {
            var leave = _layout.Rect(852, 656, 96, 108);
            _leaveSkinButton.SetBounds(leave.X, leave.Y, leave.Width, leave.Height);
        }

        var status = _layout.Rect(636, 742, 312, 26);
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
