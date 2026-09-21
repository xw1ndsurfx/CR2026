using Intersect.Client.Framework.Graphics;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Blackjack;
using Intersect.Network.Packets.MiniGames;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;

namespace Intersect.Client.Interface.Game;

internal sealed partial class BlackjackWindow
{
    private enum BlackjackMotionKind
    {
        DealCard,
        DealerCard,
        ChipToTable,
        ChipToPlayer,
        Reveal,
        Shuffle,
        ResultPulse,
    }

    private sealed class BlackjackMotion
    {
        public required BlackjackMotionKind Kind;
        public required long Starts;
        public required int Duration;
        public required float FromX;
        public required float FromY;
        public required float ToX;
        public required float ToY;
    }

    private readonly List<BlackjackMotion> _proceduralMotions = [];
    private bool _motionInitialized;
    private long _motionHand = -1;
    private BlackjackStage _motionStage = BlackjackStage.Waiting;
    private int _motionDealerCards;
    private bool _motionDealerHidden;
    private readonly Dictionary<(Guid Player, int Hand), int> _motionCardCounts = [];
    private readonly Dictionary<(Guid Player, int Hand), long> _motionBets = [];

    private static PokerMotionSet BlackjackMotionSettings(BlackjackTableState state) => new(
        state.ProceduralAnimationSpeed,
        state.AnimateDealCards,
        state.AnimateBoardCards,
        state.AnimateChips,
        state.AnimateShowdown,
        state.AnimateShuffle,
        state.AnimateAllIn);

    private void UpdateProceduralMotions(BlackjackTableState state, BlackjackPlayerState me)
    {
        var settings = BlackjackMotionSettings(state);

        // The first authoritative snapshot, including reconnect, is always rendered at rest.
        // Historical actions are never reconstructed or replayed.
        if (!_motionInitialized)
        {
            _motionInitialized = true;
            SnapBlackjackMotionState(state);
            return;
        }

        if (!settings.Enabled)
        {
            _proceduralMotions.Clear();
            SnapBlackjackMotionState(state);
            return;
        }

        var now = Environment.TickCount64;
        var newRound = state.HandId > 0 && state.HandId != _motionHand &&
            state.Stage is >= BlackjackStage.Betting and <= BlackjackStage.Finished;

        if (newRound)
        {
            _proceduralMotions.Clear();
            if (settings.Shuffle)
                AddBlackjackMotion(BlackjackMotionKind.Shuffle, now, settings.Duration(520), 500, 240, 500, 240);

            if (settings.DealCards && state.Stage >= BlackjackStage.Players)
            {
                var delay = settings.Shuffle ? settings.Duration(260) : 0;
                var step = Math.Max(45, settings.Duration(115));
                var index = 0;
                foreach (var player in state.Seats.Where(s => s.InRound && s.Hands.Length > 0).OrderBy(s => s.Seat))
                {
                    var slot = (player.Seat - me.Seat + 5) % 5;
                    var panel = Panel(slot);
                    for (var hand = 0; hand < player.Hands.Length; ++hand)
                    for (var card = 0; card < player.Hands[hand].Cards.Length; ++card)
                    {
                        var targetX = panel.X + hand * panel.W / 2 + 28 + card * 22;
                        var targetY = panel.Y + 101;
                        AddBlackjackMotion(BlackjackMotionKind.DealCard, now + delay + index++ * step,
                            settings.Duration(340), 500, 240, targetX, targetY);
                    }
                }

                if (state.DealerCards.Length > 0)
                    AddBlackjackMotion(BlackjackMotionKind.DealerCard, now + delay + index * step,
                        settings.Duration(340), 500, 240, 500, 172);
            }
        }
        else if (state.HandId == _motionHand)
        {
            if (settings.DealCards)
            {
                foreach (var player in state.Seats)
                {
                    var slot = (player.Seat - me.Seat + 5) % 5;
                    var panel = Panel(slot);
                    for (var hand = 0; hand < player.Hands.Length; ++hand)
                    {
                        var key = (player.PlayerId, hand);
                        var oldCount = _motionCardCounts.GetValueOrDefault(key);
                        var currentCount = player.Hands[hand].Cards.Length;
                        for (var card = oldCount; card < currentCount; ++card)
                            AddBlackjackMotion(BlackjackMotionKind.DealCard,
                                now + (card - oldCount) * Math.Max(40, settings.Duration(90)),
                                settings.Duration(330), 500, 240,
                                panel.X + hand * panel.W / 2 + 28 + card * 22, panel.Y + 101);
                    }
                }
            }

            if (settings.Chips)
            {
                foreach (var player in state.Seats)
                {
                    var slot = (player.Seat - me.Seat + 5) % 5;
                    var panel = Panel(slot);
                    for (var hand = 0; hand < player.Hands.Length; ++hand)
                    {
                        var key = (player.PlayerId, hand);
                        var oldBet = _motionBets.GetValueOrDefault(key);
                        if (player.Hands[hand].Bet > oldBet)
                            AddBlackjackMotion(BlackjackMotionKind.ChipToTable, now,
                                settings.Duration(420), panel.X + panel.W / 2, panel.Y + 54, 500, 292);
                    }
                }
            }

            var dealerAdded = state.DealerCards.Length > _motionDealerCards;
            var revealed = _motionDealerHidden && !state.DealerHoleHidden;
            var initialDealerCard = _motionStage == BlackjackStage.Betting && state.Stage == BlackjackStage.Players;
            if (dealerAdded && (initialDealerCard ? settings.DealCards : settings.BoardCards))
            {
                for (var i = _motionDealerCards; i < state.DealerCards.Length; ++i)
                    AddBlackjackMotion(BlackjackMotionKind.DealerCard,
                        now + (i - _motionDealerCards) * Math.Max(55, settings.Duration(110)),
                        settings.Duration(380), 500, 240, 500 + (i - 1) * 38, 172);
            }
            if (revealed && settings.Showdown)
                AddBlackjackMotion(BlackjackMotionKind.Reveal, now, settings.Duration(520), 500, 172, 500, 172);

            var enteredFinished = _motionStage != BlackjackStage.Finished && state.Stage == BlackjackStage.Finished;
            if (enteredFinished)
            {
                if (settings.Chips)
                {
                    var payout = 0;
                    foreach (var player in state.Seats.Where(s => s.Hands.Sum(h => h.Net) > 0))
                    {
                        var slot = (player.Seat - me.Seat + 5) % 5;
                        var panel = Panel(slot);
                        AddBlackjackMotion(BlackjackMotionKind.ChipToPlayer,
                            now + payout++ * Math.Max(65, settings.Duration(130)),
                            settings.Duration(520), 500, 292, panel.X + panel.W / 2, panel.Y + 54);
                    }
                }

                if (settings.Showdown && me.Hands.Sum(h => h.Net) > 0)
                {
                    var panel = Panel(0);
                    AddBlackjackMotion(BlackjackMotionKind.ResultPulse, now, settings.Duration(720),
                        panel.X + panel.W / 2, panel.Y + 68, panel.X + panel.W / 2, panel.Y + 68);
                }
            }
        }

        _proceduralMotions.RemoveAll(m => now > m.Starts + m.Duration + 150);
        if (_proceduralMotions.Count > 64)
            _proceduralMotions.RemoveRange(0, _proceduralMotions.Count - 64);

        SnapBlackjackMotionState(state);
    }

    private void SnapBlackjackMotionState(BlackjackTableState state)
    {
        _motionHand = state.HandId;
        _motionStage = state.Stage;
        _motionDealerCards = state.DealerCards.Length;
        _motionDealerHidden = state.DealerHoleHidden;
        _motionCardCounts.Clear();
        _motionBets.Clear();
        foreach (var player in state.Seats)
        for (var hand = 0; hand < player.Hands.Length; ++hand)
        {
            _motionCardCounts[(player.PlayerId, hand)] = player.Hands[hand].Cards.Length;
            _motionBets[(player.PlayerId, hand)] = player.Hands[hand].Bet;
        }
    }

    private void AddBlackjackMotion(BlackjackMotionKind kind, long starts, int duration,
        float fromX, float fromY, float toX, float toY)
    {
        if (duration <= 0) return;
        _proceduralMotions.Add(new()
        {
            Kind = kind, Starts = starts, Duration = duration,
            FromX = fromX, FromY = fromY, ToX = toX, ToY = toY,
        });
    }

    private void RenderProceduralMotions(RendererBase renderer)
    {
        if (_proceduralMotions.Count == 0) return;
        var now = Environment.TickCount64;
        foreach (var motion in _proceduralMotions)
        {
            if (now < motion.Starts || now > motion.Starts + motion.Duration) continue;
            var t = Math.Clamp((now - motion.Starts) / (float)Math.Max(1, motion.Duration), 0f, 1f);
            var eased = 1f - MathF.Pow(1f - t, 3f);
            var x = motion.FromX + (motion.ToX - motion.FromX) * eased;
            var y = motion.FromY + (motion.ToY - motion.FromY) * eased;
            switch (motion.Kind)
            {
                case BlackjackMotionKind.DealCard:
                case BlackjackMotionKind.DealerCard:
                    DrawBlackjackMotionCard(renderer, x, y, t);
                    break;
                case BlackjackMotionKind.ChipToTable:
                case BlackjackMotionKind.ChipToPlayer:
                    DrawBlackjackChip(renderer, x, y, t);
                    break;
                case BlackjackMotionKind.Reveal:
                    DrawBlackjackReveal(renderer, x, y, t);
                    break;
                case BlackjackMotionKind.Shuffle:
                    DrawBlackjackShuffle(renderer, t);
                    break;
                case BlackjackMotionKind.ResultPulse:
                    DrawBlackjackResultPulse(renderer, x, y, t);
                    break;
            }
        }
    }

    private void MotionFill(RendererBase renderer, Color color, int x, int y, int width, int height)
    {
        if (width < 1 || height < 1) return;
        var r = _layout.Rect(x, y, width, height);
        renderer.DrawColor = color;
        renderer.DrawFilledRect(new Rectangle(r.X, r.Y, r.Width, r.Height));
    }

    private void DrawBlackjackMotionCard(RendererBase renderer, float x, float y, float t)
    {
        var width = Math.Max(5, (int)(34 * Math.Max(0.18f, 1f - 0.25f * MathF.Sin(t * MathF.PI))));
        MotionFill(renderer, new Color(236, 235, 224), (int)x - width / 2, (int)y - 24, width, 48);
        MotionFill(renderer, new Color(92, 38, 31), (int)x - width / 2 + 3, (int)y - 21, Math.Max(1, width - 6), 42);
    }

    private void DrawBlackjackChip(RendererBase renderer, float x, float y, float t)
    {
        var spread = 2 + (int)(3 * MathF.Sin(t * MathF.PI));
        for (var i = 0; i < 4; ++i)
        {
            var color = i % 2 == 0 ? new Color(228, 206, 128) : new Color(214, 214, 214);
            MotionFill(renderer, color, (int)x - 8 + i * spread, (int)y - 5 - i * 2, 12, 7);
        }
    }

    private void DrawBlackjackReveal(RendererBase renderer, float x, float y, float t)
    {
        var width = Math.Max(4, (int)(46 * Math.Abs(MathF.Cos(t * MathF.PI))));
        MotionFill(renderer, new Color(238, 224, 177), (int)x - width / 2, (int)y - 33, width, 66);
    }

    private void DrawBlackjackShuffle(RendererBase renderer, float t)
    {
        var offset = (int)(26 * MathF.Sin(t * MathF.PI * 2f));
        DrawBlackjackMotionCard(renderer, 487 + offset, 240, t);
        DrawBlackjackMotionCard(renderer, 513 - offset, 240, 1f - t);
    }

    private void DrawBlackjackResultPulse(RendererBase renderer, float x, float y, float t)
    {
        var size = 62 + (int)(28 * MathF.Sin(t * MathF.PI));
        var thickness = Math.Max(2, (int)(4 * (1f - t)));
        var left = (int)x - size / 2;
        var top = (int)y - size / 2;
        var color = new Color(242, 200, 88);
        MotionFill(renderer, color, left, top, size, thickness);
        MotionFill(renderer, color, left, top + size - thickness, size, thickness);
        MotionFill(renderer, color, left, top, thickness, size);
        MotionFill(renderer, color, left + size - thickness, top, thickness, size);
    }
}
