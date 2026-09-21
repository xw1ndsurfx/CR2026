using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.MiniGames;

namespace Intersect.Client.Interface.Game;

internal sealed partial class PokerWindow
{
    private Label? _currencyFooter;
    private void UpdateCurrency(PokerTableState state, PokerPlayerState me)
    {
        _currencyFooter ??= _content.Children.OfType<Label>().Single(l => l.Name == "TestOnly");
        if (state.CurrencyItemId == Guid.Empty)
        { _currencyFooter.Text = Strings.PokerScene.TestProgress; return; }
        var currency = Short(ItemDescriptor.GetName(state.CurrencyItemId), 24);
        _currencyFooter.Text = Strings.PokerCurrency.Funded.ToString(currency, me.Chips);
        for (var i = 0; i < 6; ++i)
        {
            var seat = state.Seats.FirstOrDefault(s => PokerSceneLayout.Slot(s.Seat, me.Seat) == i);
            _stacks[i].Text = seat == null ? "" : Strings.PokerCurrency.Stack.ToString(seat.Chips, seat.StreetBet);
        }
        if (state.NetWin > 0) _payouts.Text = Strings.PokerCurrency.Net.ToString(state.NetWin, currency);
        var opponents = state.Seats.Any(s => s.PlayerId != me.PlayerId && !s.Leaving && s.Chips > 0);
        _start.IsDisabled |= !opponents;
        if (!state.MoneyPending) return;
        _turn.Text = Strings.PokerCurrency.Pending;
        _start.IsDisabled = _fold.IsDisabled = _check.IsDisabled = _call.IsDisabled = true;
        _raise.IsDisabled = _allIn.IsDisabled = _minimum.IsDisabled = _amount.IsDisabled = true;
        _back.IsDisabled = true;
        foreach (var button in _backs) button.IsDisabled = true;
    }
}
