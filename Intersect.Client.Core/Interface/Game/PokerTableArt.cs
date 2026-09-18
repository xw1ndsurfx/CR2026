using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Network.Packets.MiniGames;

namespace Intersect.Client.Interface.Game;

/// <summary>Optional fixed assets; all card identities and back IDs come from the server.</summary>
internal sealed class PokerTableArt
{
    private readonly Dictionary<string, IGameTexture?> _cards = new(StringComparer.Ordinal);
    private readonly ImagePanel[] _board = new ImagePanel[5];
    private readonly ImagePanel[] _own = new ImagePanel[2];
    private readonly ImagePanel[,] _seats = new ImagePanel[6, 2];
    private readonly ImagePanel _preview;
    private readonly Label[] _boardLabels;
    private readonly PokerDealTracker _deals = new();
    private readonly Effect[] _effects;
    private long _started;
    public bool SelectedBackMissing { get; private set; }

    private sealed class Effect
    {
        public required ImagePanel Image;
        public int Columns, Rows, Count, Speed;
    }

    public PokerTableArt(Base parent, Label[] boardLabels)
    {
        _boardLabels = boardLabels;
        for (var i = 0; i < 5; ++i) _board[i] = Image(parent, "PokerBoardArt" + i);
        for (var i = 0; i < 2; ++i) _own[i] = Image(parent, "PokerOwnArt" + i);
        for (var seat = 0; seat < 6; ++seat)
            for (var i = 0; i < 2; ++i) _seats[seat, i] = Image(parent, $"PokerSeatArt{seat}_{i}");
        _preview = Image(parent, "PokerBackPreview");
        _effects = [new() { Image = Image(parent, "PokerDealLower") }, new() { Image = Image(parent, "PokerDealUpper") }];
    }

    public void Update(PokerTableState state, Guid player, Guid table)
    {
        for (var i = 0; i < 5; ++i)
        {
            var texture = i < state.Board.Length ? Card(state.Board[i]) : null;
            Fit(_board[i], texture, 166 + i * 86, 182, 48, 64);
            _boardLabels[i].IsHidden = texture != null;
        }
        for (var i = 0; i < 2; ++i)
            Fit(_own[i], i < state.MyCards.Length ? Card(state.MyCards[i]) : null,
                440 + i * 58, 358, 48, 64);
        for (var seatIndex = 0; seatIndex < 6; ++seatIndex)
        {
            var seat = state.Seats.FirstOrDefault(s => s.Seat == seatIndex);
            for (var i = 0; i < 2; ++i)
            {
                IGameTexture? texture = null;
                if (seat is { InHand: true, Folded: false })
                {
                    var visible = seat.PlayerId == player ? state.MyCards : seat.RevealedCards;
                    texture = i < visible.Length ? Card(visible[i]) : Back(seat.CardBackId);
                }
                Fit(_seats[seatIndex, i], texture, 8 + seatIndex % 3 * 240 + 176 + i * 25,
                    (seatIndex < 3 ? 72 : 282) + 45, 22, 30);
            }
        }
        var selected = state.Seats.First(s => s.PlayerId == player).SelectedCardBackId;
        SelectedBackMissing = Lookup(PokerCardAssets.BackFileName(selected)) == null;
        Fit(_preview, Back(selected), 340, 558, 48, 64);
        if (_deals.Observe(table, state.HandId, state.Board.Length)) BeginAnimation(state.DealAnimationId);
        AdvanceAnimation();
    }

    private IGameTexture? Card(int card) => PokerCardAssets.FileNameFor(card) is { } file ? Lookup(file) : null;
    private IGameTexture? Back(int id) => Lookup(PokerCardAssets.BackFileName(id)) ?? Lookup(PokerCardAssets.Back);
    private IGameTexture? Lookup(string file)
    {
        if (!_cards.TryGetValue(file, out var texture))
        {
            texture = GameContentManager.Current?.GetTexture(TextureType.Misc, file);
            _cards.Add(file, texture);
        }
        return texture;
    }

    private void BeginAnimation(Guid id)
    {
        foreach (var effect in _effects) { effect.Count = 0; effect.Image.IsHidden = true; }
        if (id == Guid.Empty || AnimationDescriptor.Get(id) is not { } animation) return;
        Configure(_effects[0], animation.Lower);
        Configure(_effects[1], animation.Upper);
        _started = Environment.TickCount64;
    }

    private static void Configure(Effect effect, AnimationLayer? layer)
    {
        if (layer == null || string.IsNullOrWhiteSpace(layer.Sprite) || layer.XFrames < 1 ||
            layer.XFrames > 128 || layer.YFrames < 1 || layer.YFrames > 128 || layer.FrameCount < 1) return;
        var texture = GameContentManager.Current?.GetTexture(TextureType.Animation, layer.Sprite);
        if (texture == null || texture.Width < layer.XFrames || texture.Height < layer.YFrames) return;
        effect.Image.Texture = texture;
        effect.Columns = layer.XFrames;
        effect.Rows = layer.YFrames;
        effect.Count = Math.Min(layer.FrameCount, layer.XFrames * layer.YFrames);
        effect.Speed = Math.Clamp(layer.FrameSpeed, 10, 1000);
        effect.Image.SetBounds(272, 152, 184, 112);
    }

    private void AdvanceAnimation()
    {
        var elapsed = Math.Max(0, Environment.TickCount64 - _started);
        foreach (var effect in _effects)
        {
            var frame = effect.Speed > 0 ? elapsed / effect.Speed : long.MaxValue;
            if (effect.Count == 0 || frame >= effect.Count || elapsed >= 8000 || effect.Image.Texture is not { } texture)
            { effect.Image.IsHidden = true; continue; }
            effect.Image.SetTextureRect((int)(frame % effect.Columns) * (texture.Width / effect.Columns),
                (int)(frame / effect.Columns) * (texture.Height / effect.Rows),
                texture.Width / effect.Columns, texture.Height / effect.Rows);
            effect.Image.IsHidden = false;
        }
    }

    private static ImagePanel Image(Base parent, string name) => new(parent, name)
    { MouseInputEnabled = false, KeyboardInputEnabled = false, ShouldDrawBackground = false, IsHidden = true };

    private static void Fit(ImagePanel image, IGameTexture? texture, int x, int y, int width, int height)
    {
        image.Texture = texture;
        image.IsHidden = texture == null || texture.Width < 1 || texture.Height < 1;
        if (image.IsHidden || texture == null) return;
        var scale = Math.Min(width / (float)texture.Width, height / (float)texture.Height);
        var w = Math.Max(1, (int)(texture.Width * scale));
        var h = Math.Max(1, (int)(texture.Height * scale));
        image.SetBounds(x + (width - w) / 2, y + (height - h) / 2, w, h);
    }
}
