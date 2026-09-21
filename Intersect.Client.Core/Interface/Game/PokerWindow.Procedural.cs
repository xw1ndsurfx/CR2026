using Intersect.Client.Framework.Graphics;
using Intersect.Framework.Core.MiniGames;
using Intersect.Network.Packets.MiniGames;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;

namespace Intersect.Client.Interface.Game;

internal sealed partial class PokerWindow
{
    private enum ProceduralMotionKind
    {
        DealCard,
        BoardCard,
        ChipToPot,
        PotToWinner,
        Showdown,
        Shuffle,
        AllIn,
    }

    private sealed class ProceduralMotion
    {
        public required ProceduralMotionKind Kind;
        public required long Starts;
        public required int Duration;
        public required float FromX;
        public required float FromY;
        public required float ToX;
        public required float ToY;
        public int Index;
    }

    private readonly List<ProceduralMotion> _proceduralMotions = [];
    private bool _proceduralInitialized;
    private long _proceduralHand = -1;
    private long _proceduralDecision;
    private PokerStage _proceduralStage = PokerStage.Waiting;
    private int _proceduralBoardCount;

    private static PokerMotionSet MotionSettings(PokerTableState state) => new(
        state.ProceduralAnimationSpeed,
        state.AnimateDealCards,
        state.AnimateBoardCards,
        state.AnimateChips,
        state.AnimateShowdown,
        state.AnimateShuffle,
        state.AnimateAllIn);

    private void UpdateProceduralMotions(PokerTableState state, PokerPlayerState me)
    {
        var settings = MotionSettings(state);
        var lastDecision = state.Decisions.LastOrDefault()?.Sequence ?? 0;

        // A reconnect/snapshot starts from the authoritative final position. Historical
        // state is never replayed, so cosmetic motion cannot desynchronize gameplay.
        if (!_proceduralInitialized)
        {
            _proceduralInitialized = true;
            SnapProceduralState(state, lastDecision);
            return;
        }

        if (!settings.Enabled)
        {
            _proceduralMotions.Clear();
            SnapProceduralState(state, lastDecision);
            return;
        }

        var now = Environment.TickCount64;
        var newHand = state.HandId > 0 && state.HandId != _proceduralHand &&
            state.Stage is >= PokerStage.PreFlop and <= PokerStage.River;

        if (newHand)
        {
            _proceduralMotions.Clear();
            if (settings.Shuffle)
                AddMotion(ProceduralMotionKind.Shuffle, now, settings.Duration(520), 500, 246, 500, 246);

            if (settings.DealCards)
            {
                var active = state.Seats
                    .Where(s => s.InHand && !s.Leaving)
                    .OrderBy(s => SeatDistance(s.Seat, state.DealerSeat))
                    .ToArray();
                var delay = settings.Shuffle ? settings.Duration(280) : 0;
                var step = Math.Max(45, settings.Duration(120));
                var index = 0;
                for (var card = 0; card < 2; ++card)
                foreach (var seat in active)
                {
                    var slot = PokerSceneLayout.Slot(seat.Seat, me.Seat);
                    var target = PokerSceneLayout.Cards(slot);
                    AddMotion(ProceduralMotionKind.DealCard, now + delay + index++ * step,
                        settings.Duration(360), 500, 246, target.X + card * 58 + 26, target.Y + 35, card);
                }
            }
        }

        if (state.HandId == _proceduralHand && settings.BoardCards && state.Board.Length > _proceduralBoardCount)
        {
            var first = _proceduralBoardCount;
            for (var i = first; i < state.Board.Length; ++i)
                AddMotion(ProceduralMotionKind.BoardCard, now + (i - first) * Math.Max(55, settings.Duration(110)),
                    settings.Duration(420), 500, 246, 361 + i * 66, 339, i);
        }

        foreach (var decision in state.Decisions.Where(d => d.Sequence > _proceduralDecision).OrderBy(d => d.Sequence))
        {
            var seat = state.Seats.FirstOrDefault(s => s.PlayerId == decision.PlayerId);
            if (seat == null) continue;
            var slot = PokerSceneLayout.Slot(seat.Seat, me.Seat);
            var center = PokerSceneLayout.Center(slot);
            var allIn = seat.AllIn && decision.Action is "call" or "raise";

            if (settings.Chips && decision.Action is "call" or "raise")
                AddMotion(ProceduralMotionKind.ChipToPot, now, settings.Duration(430),
                    center.X, center.Y + 15, 500, 286);

            if (settings.AllIn && allIn)
                AddMotion(ProceduralMotionKind.AllIn, now, settings.Duration(720),
                    center.X, center.Y, center.X, center.Y);
        }

        var enteredFinished = _proceduralHand == state.HandId &&
            _proceduralStage != PokerStage.Finished && state.Stage == PokerStage.Finished;
        if (enteredFinished)
        {
            if (settings.Showdown)
            {
                var revealIndex = 0;
                foreach (var seat in state.Seats.Where(s => s.RevealedCards.Length > 0 && !s.Folded))
                {
                    var slot = PokerSceneLayout.Slot(seat.Seat, me.Seat);
                    var cards = PokerSceneLayout.Cards(slot);
                    AddMotion(ProceduralMotionKind.Showdown, now + revealIndex++ * Math.Max(60, settings.Duration(130)),
                        settings.Duration(520), cards.X + 55, cards.Y + 35, cards.X + 55, cards.Y + 35);
                }
            }

            if (settings.Chips)
            {
                var payoutIndex = 0;
                foreach (var payout in state.Payouts.Where(p => !p.IsRefund && p.Chips > 0))
                {
                    var winner = state.Seats.FirstOrDefault(s => s.PlayerId == payout.PlayerId);
                    if (winner == null) continue;
                    var slot = PokerSceneLayout.Slot(winner.Seat, me.Seat);
                    var center = PokerSceneLayout.Center(slot);
                    AddMotion(ProceduralMotionKind.PotToWinner,
                        now + payoutIndex++ * Math.Max(70, settings.Duration(140)), settings.Duration(560),
                        500, 286, center.X, center.Y + 15);
                }
            }
        }

        _proceduralMotions.RemoveAll(m => now > m.Starts + m.Duration + 150);
        if (_proceduralMotions.Count > 64)
            _proceduralMotions.RemoveRange(0, _proceduralMotions.Count - 64);

        SnapProceduralState(state, lastDecision);
    }

    private void SnapProceduralState(PokerTableState state, long lastDecision)
    {
        _proceduralHand = state.HandId;
        _proceduralStage = state.Stage;
        _proceduralBoardCount = state.Board.Length;
        _proceduralDecision = lastDecision;
    }

    private static int SeatDistance(int seat, int dealer)
    {
        if (dealer < 0) return seat;
        var distance = (seat - dealer + 6) % 6;
        return distance == 0 ? 6 : distance;
    }

    private void AddMotion(ProceduralMotionKind kind, long starts, int duration,
        float fromX, float fromY, float toX, float toY, int index = 0)
    {
        if (duration <= 0) return;
        _proceduralMotions.Add(new()
        {
            Kind = kind,
            Starts = starts,
            Duration = duration,
            FromX = fromX,
            FromY = fromY,
            ToX = toX,
            ToY = toY,
            Index = index,
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
                case ProceduralMotionKind.DealCard:
                    DrawMotionCard(renderer, x, y, 34, 48, false, t);
                    break;
                case ProceduralMotionKind.BoardCard:
                    DrawMotionCard(renderer, x, y, 38, 52, true, t);
                    break;
                case ProceduralMotionKind.ChipToPot:
                case ProceduralMotionKind.PotToWinner:
                    DrawChipStack(renderer, x, y, t);
                    break;
                case ProceduralMotionKind.Showdown:
                    DrawFlipPulse(renderer, x, y, t);
                    break;
                case ProceduralMotionKind.Shuffle:
                    DrawShuffle(renderer, t);
                    break;
                case ProceduralMotionKind.AllIn:
                    DrawAllInPulse(renderer, x, y, t);
                    break;
            }
        }
    }

    private void DrawMotionCard(RendererBase renderer, float x, float y, int width, int height, bool flip, float t)
    {
        var flipScale = flip ? Math.Max(0.10f, Math.Abs(1f - 2f * Math.Clamp(t * 1.35f, 0f, 1f))) : 1f;
        var w = Math.Max(4, (int)(width * flipScale));
        Fill(renderer, new Color(235, 235, 226), (int)x - w / 2, (int)y - height / 2, w, height);
        Fill(renderer, new Color(92, 38, 31), (int)x - w / 2 + 3, (int)y - height / 2 + 3,
            Math.Max(1, w - 6), Math.Max(1, height - 6));
    }

    private void DrawChipStack(RendererBase renderer, float x, float y, float t)
    {
        var spread = 2 + (int)(3 * MathF.Sin(t * MathF.PI));
        for (var i = 0; i < 4; ++i)
        {
            var color = i % 2 == 0 ? new Color(228, 206, 128) : new Color(214, 214, 214);
            Fill(renderer, color, (int)x - 8 + i * spread, (int)y - 5 - i * 2, 12, 7);
        }
    }

    private void DrawFlipPulse(RendererBase renderer, float x, float y, float t)
    {
        var width = Math.Max(3, (int)(48 * Math.Abs(MathF.Cos(t * MathF.PI))));
        Fill(renderer, new Color(238, 224, 177), (int)x - width / 2, (int)y - 38, width, 76);
    }

    private void DrawShuffle(RendererBase renderer, float t)
    {
        var offset = (int)(28 * MathF.Sin(t * MathF.PI * 2f));
        DrawMotionCard(renderer, 487 + offset, 246, 32, 46, false, t);
        DrawMotionCard(renderer, 513 - offset, 246, 32, 46, false, 1f - t);
    }

    private void DrawAllInPulse(RendererBase renderer, float x, float y, float t)
    {
        var size = 58 + (int)(28 * MathF.Sin(t * MathF.PI));
        var thickness = Math.Max(2, (int)(4 * (1f - t)));
        var left = (int)x - size / 2;
        var top = (int)y - size / 2;
        var color = new Color(242, 200, 88);
        Fill(renderer, color, left, top, size, thickness);
        Fill(renderer, color, left, top + size - thickness, size, thickness);
        Fill(renderer, color, left, top, thickness, size);
        Fill(renderer, color, left + size - thickness, top, thickness, size);
    }
}
