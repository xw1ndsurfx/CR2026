using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.WorldMaps;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class WorldMapLegendView : ScrollControl
{
    private string? _displayedJson;
    private WorldMapLegend? _source;
    private readonly List<Base> _rows = [];

    public WorldMapLegendView(Base parent) : base(parent, nameof(WorldMapLegendView))
    {
        MouseInputEnabled = true;
        KeyboardInputEnabled = false;
        EnableScroll(false, true);
    }

    protected override void OnPreDraw(SkinBase skin)
    {
        base.OnPreDraw(skin);
        if (ReferenceEquals(_source, Globals.WorldMapLegend)) return;
        _source = Globals.WorldMapLegend;
        var json = Globals.WorldMapLegend.ToJson();
        if (json == _displayedJson) return;
        _displayedJson = json;
        foreach (var row in _rows) RemoveChild(row, true);
        _rows.Clear();
        AddRow("Legend", Guid.Empty, null, 0);
        AddRow("You", Guid.Empty, Color.White, 28);
        AddRow("Party", Guid.Empty, WorldMapWindow.WorldMapCanvas.PartyMarkerColor, 56);
        AddRow("Friends", Guid.Empty, WorldMapWindow.WorldMapCanvas.FriendMarkerColor, 84);
        AddRow("Guild", Guid.Empty, WorldMapWindow.WorldMapCanvas.GuildMarkerColor, 112);
        var y = 144;
        foreach (var entry in Globals.WorldMapLegend.Entries)
        {
            AddRow(entry.Label, entry.AnimationId, null, y);
            y += 54;
        }
        SetInnerSize(190, y);
    }

    private void AddRow(string text, Guid animationId, Color? color, int y)
    {
        var row = new Base(this) { MouseInputEnabled = false };
        row.SetBounds(0, y, 190, animationId == Guid.Empty ? 28 : 52);
        _rows.Add(row);
        if (animationId != Guid.Empty || color != null)
        {
            var icon = new LegendIcon(row, animationId, color);
            icon.SetBounds(0, 0, animationId == Guid.Empty ? 34 : 52, row.Height);
        }
        var label = new Label(row)
        {
            Text = text, Font = Skin.DefaultFont, FontSize = 10,
            AutoSizeToContents = false, WrappingBehavior = WrappingBehavior.Wrapped,
            TextColorOverride = Color.White, MouseInputEnabled = false,
        };
        var labelX = animationId == Guid.Empty && color == null ? 4 : animationId == Guid.Empty ? 38 : 56;
        var labelY = animationId == Guid.Empty ? 5 : 17;
        label.SetBounds(labelX, labelY, 190 - labelX, row.Height - labelY);
    }

    private sealed class LegendIcon(Base parent, Guid animationId, Color? color) : Base(parent)
    {
        protected override void Render(SkinBase skin)
        {
            if (color != null)
                WorldMapWindow.WorldMapCanvas.DrawSocialLocation(skin.Renderer, 17, 14, color,
                    color == WorldMapWindow.WorldMapCanvas.GuildMarkerColor);
            else if (AnimationDescriptor.TryGet(animationId, out var animation))
            {
                WorldMapWindow.WorldMapCanvas.DrawAnimationLayerMarker(skin.Renderer, animation.Lower, 26, 26, 48, fitVisible: true);
                WorldMapWindow.WorldMapCanvas.DrawAnimationLayerMarker(skin.Renderer, animation.Upper, 26, 26, 48, fitVisible: true);
            }
        }
    }
}
