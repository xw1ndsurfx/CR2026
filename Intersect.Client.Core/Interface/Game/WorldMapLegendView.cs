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
        AddRow("You", Guid.Empty, Color.White, 36);
        AddRow("Party", Guid.Empty, WorldMapWindow.WorldMapCanvas.PartyMarkerColor, 72);
        AddRow("Friends", Guid.Empty, WorldMapWindow.WorldMapCanvas.FriendMarkerColor, 108);
        var y = 148;
        foreach (var entry in Globals.WorldMapLegend.Entries)
        {
            AddRow(entry.Label, entry.AnimationId, null, y);
            y += 82;
        }
        SetInnerSize(190, y);
    }

    private void AddRow(string text, Guid animationId, Color? color, int y)
    {
        var row = new Base(this) { MouseInputEnabled = false };
        row.SetBounds(0, y, 190, animationId == Guid.Empty ? 34 : 80);
        _rows.Add(row);
        if (animationId != Guid.Empty || color != null)
        {
            var icon = new LegendIcon(row, animationId, color);
            icon.SetBounds(0, 0, 34, 34);
        }
        var label = new Label(row)
        {
            Text = text, Font = Skin.DefaultFont, FontSize = 10,
            AutoSizeToContents = false, WrappingBehavior = WrappingBehavior.Wrapped,
            TextColorOverride = Color.White, MouseInputEnabled = false,
        };
        label.SetBounds(animationId == Guid.Empty && color == null ? 4 : 38, 7, 148, row.Height - 7);
    }

    private sealed class LegendIcon(Base parent, Guid animationId, Color? color) : Base(parent)
    {
        protected override void Render(SkinBase skin)
        {
            if (color != null)
                WorldMapWindow.WorldMapCanvas.DrawLocationMarker(skin.Renderer, 17, 17, color);
            else if (AnimationDescriptor.TryGet(animationId, out var animation))
            {
                WorldMapWindow.WorldMapCanvas.DrawAnimationLayerMarker(skin.Renderer, animation.Lower, 17, 17);
                WorldMapWindow.WorldMapCanvas.DrawAnimationLayerMarker(skin.Renderer, animation.Upper, 17, 17);
            }
        }
    }
}
