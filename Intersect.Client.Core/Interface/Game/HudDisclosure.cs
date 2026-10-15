using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;

namespace Intersect.Client.Interface.Game;

internal sealed class HudDisclosure
{
    private readonly Base _panel;
    private readonly Base _title;
    private readonly Button _toggle;
    private readonly string _preference;
    private readonly int _expandedHeight;
    private readonly Dictionary<Base, bool> _visibility = [];
    internal bool Collapsed { get; private set; }

    internal HudDisclosure(Base panel, Base title, string preference)
    {
        _panel = panel; _title = title; _preference = preference; _expandedHeight = panel.Height;
        _toggle = new Button(panel, "HudCollapse") { Text = "-", Font = panel.Skin.DefaultFont, FontSize = 12 };
        _toggle.SetBounds(panel.Width - 29, 5, 24, 24);
        _toggle.Clicked += (_, _) => SetCollapsed(!Collapsed);
        title.Width = Math.Min(title.Width, panel.Width - title.X - 34);
        SetCollapsed(Globals.Database?.LoadPreference(preference, false) ?? false);
    }

    private void SetCollapsed(bool collapsed)
    {
        if (collapsed && !Collapsed)
        {
            foreach (var child in _panel.Children.Where(child => child != _title && child != _toggle))
            {
                _visibility[child] = child.IsHidden;
                child.IsHidden = true;
            }
        }
        else if (!collapsed && Collapsed)
        {
            foreach (var (child, hidden) in _visibility) child.IsHidden = hidden;
            _visibility.Clear();
        }
        Collapsed = collapsed;
        _panel.Height = collapsed ? 36 : _expandedHeight;
        _toggle.Text = collapsed ? "+" : "-";
        _toggle.BringToFront();
        if (Globals.Database is { } database)
            database.GetType().GetProperty(_preference)?.SetValue(database, collapsed);
        Globals.Database?.SavePreference(_preference, collapsed);
    }
}
