using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.MiniGames;
using Intersect.Network.Packets.MiniGames;

namespace Intersect.Client.Interface.Game;

/// <summary>Art adapter for the 2D scene. Cards and unlocks remain server-owned.</summary>
internal sealed class PokerTableArt
{
    private readonly Dictionary<string, IGameTexture?> _cache = new(StringComparer.Ordinal);
    private readonly ImagePanel[] _board = new ImagePanel[5], _own = new ImagePanel[2], _portraits = new ImagePanel[6], _choices = new ImagePanel[6];
    private readonly ImagePanel _selectedBack;
    private readonly ImagePanel[,] _seats = new ImagePanel[6, 2];
    private readonly Label[,] _fallback = new Label[6, 2];
    private readonly Label[] _boardLabels;
    private readonly PokerDealTracker _deals = new();
    private readonly Effect[] _effects;
    private long _started;
    private sealed class Effect { public required ImagePanel Image; public int Columns, Rows, Count, Speed; }
    public bool SelectedBackMissing { get; private set; }
    public bool HasOwnImages { get; private set; }
    public bool HasPortrait(int slot) => !_portraits[slot].IsHidden;

    public PokerTableArt(Base parent, Base backTray, Label[] boardLabels)
    {
        _boardLabels = boardLabels;
        for (var i = 0; i < 5; ++i) _board[i] = Image(parent, "PokerBoardArt" + i);
        for (var i = 0; i < 2; ++i) _own[i] = Image(parent, "PokerOwnArt" + i);
        for (var slot = 0; slot < 6; ++slot)
        {
            _portraits[slot] = Image(parent, "PokerPortrait" + slot);
            _choices[slot] = Image(backTray, "PokerBackPreview" + slot);
            for (var card = 0; card < 2; ++card)
            {
                _seats[slot, card] = Image(parent, $"PokerSeatArt{slot}_{card}");
                _fallback[slot, card] = new Label(parent, $"PokerSeatFallback{slot}_{card}")
                { AutoSizeToContents = false, Font = parent.Skin.DefaultFont, FontSize = 12, MouseInputEnabled = false, IsHidden = true };
            }
        }
        _selectedBack = Image(parent, "PokerSelectedBackPreview");
        _effects = [new() { Image = Image(parent, "PokerDealLower") }, new() { Image = Image(parent, "PokerDealUpper") }];
    }
    public void Update(PokerTableState state, Guid player, Guid table, PokerSceneLayout layout)
    {
        var me = state.Seats.First(s => s.PlayerId == player);
        for (var i = 0; i < 5; ++i)
        {
            var texture = i < state.Board.Length ? Card(state.Board[i]) : null;
            Fit(_board[i], texture, layout.Rect(335 + i * 66, 304, 52, 70));
            _boardLabels[i].IsHidden = texture != null;
        }
        var own = PokerSceneLayout.Cards(0);
        HasOwnImages = state.MyCards.Length == 2;
        for (var i = 0; i < 2; ++i)
        {
            var texture = i < state.MyCards.Length ? Card(state.MyCards[i]) : null;
            Fit(_own[i], texture, layout.Rect(own.X + i * 58, own.Y, 52, 70));
            HasOwnImages &= texture != null;
        }
        for (var slot = 0; slot < 6; ++slot)
        {
            var seat = state.Seats.FirstOrDefault(s => PokerSceneLayout.Slot(s.Seat, me.Seat) == slot);
            var center = PokerSceneLayout.Center(slot);
            var portrait = seat == null ? null : state.NpcIds.Contains(seat.PlayerId)
                ? Lookup(PokerTableTheme.PortraitFile(PokerTableTheme.Portrait(seat.Name, seat.PlayerId == state.DealerNpcId)))
                : Lookup("poker_player.png");
            Fit(_portraits[slot], portrait, layout.Rect(center.X - 32, center.Y - 42, 64, 74));
            var position = PokerSceneLayout.Cards(slot);
            for (var i = 0; i < 2; ++i)
            {
                IGameTexture? texture = null;
                var text = "";
                if (slot != 0 && seat is { InHand: true, Folded: false })
                {
                    if (i < seat.RevealedCards.Length)
                    { texture = Card(seat.RevealedCards[i]); text = PokerCardAssets.FileNameFor(seat.RevealedCards[i])![..2]; }
                    else { texture = Back(seat.CardBackId); text = "[??]"; }
                }
                var rect = layout.Rect(position.X + i * 58, position.Y, 52, 70);
                Fit(_seats[slot, i], texture, rect);
                var label = _fallback[slot, i];
                label.Text = text; label.IsHidden = texture != null || text.Length == 0;
                label.FontSize = layout.FontSize(12);
                label.SetBounds(rect.X, rect.Y + rect.Height / 3, rect.Width, Math.Max(18, rect.Height / 2));
            }
            Fit(_choices[slot], Back(slot), layout.LocalRect(36 + slot * 108, 15, 48, 74));
        }
        var selectedBack = Back(me.SelectedCardBackId);
        Fit(_selectedBack, selectedBack, layout.Rect(662, 674, 38, 54));
        SelectedBackMissing = Lookup(PokerCardAssets.BackFileName(me.SelectedCardBackId)) == null;
        if (_deals.Observe(table, state.HandId, state.Board.Length)) BeginAnimation(state.DealAnimationId);
        AdvanceAnimation(layout);
    }
    private IGameTexture? Card(int value) => PokerCardAssets.FileNameFor(value) is { } file ? Lookup(file) : null;
    private IGameTexture? Back(int id) => Lookup(PokerCardAssets.BackFileName(id)) ?? Lookup(PokerCardAssets.Back) ?? Lookup(PokerCardAssets.LegacyBack);
    private IGameTexture? Lookup(string file)
    {
        if (!_cache.TryGetValue(file, out var result))
        { result = GameContentManager.Current?.GetTexture(TextureType.Misc, file); _cache.Add(file, result); }
        return result;
    }
    private void BeginAnimation(Guid id)
    {
        foreach (var effect in _effects) { effect.Count = 0; effect.Image.IsHidden = true; }
        if (id == Guid.Empty || AnimationDescriptor.Get(id) is not { } animation) return;
        Configure(_effects[0], animation.Lower); Configure(_effects[1], animation.Upper);
        _started = Environment.TickCount64;
    }
    private static void Configure(Effect effect, AnimationLayer? layer)
    {
        if (layer == null || string.IsNullOrWhiteSpace(layer.Sprite) || layer.XFrames is < 1 or > 128 ||
            layer.YFrames is < 1 or > 128 || layer.FrameCount < 1) return;
        var texture = GameContentManager.Current?.GetTexture(TextureType.Animation, layer.Sprite);
        if (texture == null || texture.Width < layer.XFrames || texture.Height < layer.YFrames) return;
        effect.Image.Texture = texture; effect.Columns = layer.XFrames; effect.Rows = layer.YFrames;
        effect.Count = Math.Min(layer.FrameCount, layer.XFrames * layer.YFrames); effect.Speed = Math.Clamp(layer.FrameSpeed, 10, 1000);
    }
    private void AdvanceAnimation(PokerSceneLayout layout)
    {
        var elapsed = Math.Max(0, Environment.TickCount64 - _started);
        foreach (var effect in _effects)
        {
            var frame = effect.Speed > 0 ? elapsed / effect.Speed : long.MaxValue;
            if (effect.Count == 0 || frame >= effect.Count || elapsed >= 8000 || effect.Image.Texture is not { } texture)
            { effect.Image.IsHidden = true; continue; }
            var w = texture.Width / effect.Columns; var h = texture.Height / effect.Rows;
            effect.Image.SetTextureRect((int)(frame % effect.Columns) * w, (int)(frame / effect.Columns) * h, w, h);
            var bounds = layout.Rect(360, 255, 280, 135);
            var scale = Math.Min(bounds.Width / (float)w, bounds.Height / (float)h);
            var width = Math.Max(1, (int)(w * scale)); var height = Math.Max(1, (int)(h * scale));
            effect.Image.SetBounds(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
            effect.Image.IsHidden = false;
        }
    }
    private static ImagePanel Image(Base parent, string name) => new(parent, name)
    { MouseInputEnabled = false, KeyboardInputEnabled = false, ShouldDrawBackground = false, IsHidden = true };
    private static void Fit(ImagePanel image, IGameTexture? texture, PokerSceneRect bounds)
    {
        image.Texture = texture;
        image.IsHidden = texture == null || texture.Width < 1 || texture.Height < 1;
        if (image.IsHidden || texture == null) return;
        image.ResetUVs();
        var scale = Math.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
        var w = Math.Max(1, (int)(texture.Width * scale)); var h = Math.Max(1, (int)(texture.Height * scale));
        image.SetBounds(bounds.X + (bounds.Width - w) / 2, bounds.Y + (bounds.Height - h) / 2, w, h);
    }
}
