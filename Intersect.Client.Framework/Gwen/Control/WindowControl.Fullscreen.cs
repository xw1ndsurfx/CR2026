using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.File_Management;

namespace Intersect.Client.Framework.Gwen.Control;

public partial class WindowControl
{
    private Point _fullscreenContentSize;
    public ScrollControl? FullscreenViewport { get; private set; }
    private ComboBox? _fullscreenPicker;
    private WindowControl[] _fullscreenPeers = [];
    private string?[] _fullscreenPeerTitles = [];
    private bool _updatingFullscreenPeers;
    private Button? _fullscreenClose;
    private Action? _fullscreenCloseAction;

    public void SetFullscreenPeers(IReadOnlyList<WindowControl> peers)
    {
        if (FullscreenViewport == null) return;
        if (peers.Count < 2)
        {
            _fullscreenPicker?.Hide();
            _titlebar.Label.Show();
            _fullscreenPeers = [];
            return;
        }
        if (_fullscreenPicker == null)
        {
            var picker = new ComboBox(_titlebar, "OpenMobileWindows")
            {
                AutoSizeToContents = false, Font = _titlebar.Label.Font ?? Skin.DefaultFont,
                FontSize = 14, TextColorOverride = Color.White,
            };
            _fullscreenPicker = picker;
            picker.ItemSelected += (_, _) =>
            {
                if (!_updatingFullscreenPeers && picker.SelectedItem?.UserData is WindowControl selected && selected.IsVisibleInTree)
                { selected.BringToFront(); selected.Focus(); }
            };
        }
        _fullscreenPicker.Show();
        _titlebar.Label.Hide();
        _fullscreenPicker.SetBounds(8, 4, Math.Max(100, Width - 152), 40);
        if (_fullscreenPeers.SequenceEqual(peers) && _fullscreenPeerTitles.SequenceEqual(peers.Select(p => p.Title))) return;
        _updatingFullscreenPeers = true;
        try
        {
            _fullscreenPicker.DeleteAll();
            foreach (var peer in peers)
            {
                var item = _fullscreenPicker.AddItem(peer.Title ?? peer.Name, userData: peer);
                item.FontSize = 14;
                item.MinimumSize = new Point(0, 44);
                if (peer == this) _fullscreenPicker.SelectedItem = item;
            }
            _fullscreenPeers = peers.ToArray();
            _fullscreenPeerTitles = peers.Select(p => p.Title).ToArray();
        }
        finally { _updatingFullscreenPeers = false; }
    }

    /// <summary>
    /// Keep the existing content/owners intact inside a clipped viewport. Fixed-size
    /// desktop layouts can then scroll without shrinking their fonts or hit targets.
    /// </summary>
    public void PresentFullscreen(Rectangle bounds, string? closeCaption = null, Action? closeAction = null)
    {
        if (FullscreenViewport == null)
        {
            var content = InnerPanel;
            _fullscreenContentSize = new Point(Math.Max(Math.Max(Width, MinimumSize.X), content.Width),
                Math.Max(Math.Max(Height, MinimumSize.Y) - _titlebar.Height, content.Height));
            MinimumSize = new Point(100, 40);
            MaximumSize = default;
            content.Parent = null;
            _innerPanel = null;
            var viewport = new ScrollControl(this, "FullscreenViewport")
            {
                OverflowX = OverflowBehavior.Auto,
                OverflowY = OverflowBehavior.Auto,
                ClipContents = true,
                ShouldCacheToTexture = false,
            };
            content.Parent = viewport;
            content.Dock = Pos.None;
            content.MinimumSize = default;
            content.MaximumSize = default;
            _innerPanel = content;
            FullscreenViewport = viewport;
            Alignment = [];
            Dock = Pos.None;
            Padding = default;
            ClipContents = true;
            ShouldCacheToTexture = false;
            DisableResizing();
            DrawShadow = false;
            _titlebar.Target = null!;
            _titlebar.MinimumSize = new Point(0, 48);
            _titlebar.Label.FontSize = Math.Max(14, _titlebar.Label.FontSize);
            _titlebar.Label.Font = GameContentManager.Current?.GetFont("sourcesansproblack") ?? Skin.DefaultFont;
            _titlebar.Label.TextColorOverride = Color.White;
            _titlebar.Label.Dock = Pos.None;
            _fullscreenClose = new FullscreenCloseButton(_titlebar)
            {
                Font = _titlebar.Label.Font ?? Skin.DefaultFont, FontSize = 14,
                TextColorOverride = Color.White,
            };
            _fullscreenClose.Clicked += (_, _) =>
            {
                if (_fullscreenCloseAction is { } action) action();
                else Close();
            };
        }

        _fullscreenCloseAction = closeAction;
        _fullscreenClose!.Text = closeCaption ?? "\u00d7";
        MinimumSize = new Point(100, 40);
        if (CurAlignments.Count != 0) Alignment = [];
        SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        LayoutFullscreenContent();
    }

    private void LayoutFullscreenContent()
    {
        if (FullscreenViewport is not { } viewport) return;
        // Desktop JSON can hide the whole titlebar, and transaction inventory
        // hides the native close icon. The mobile exit must survive both.
        _titlebar.Show();
        _titlebar.Icon.Hide();
        _titlebar.MaximumSize = default;
        _titlebar.Margin = default;
        _titlebar.SetBounds(0, 0, Width, 48);
        _titlebar.CloseButton.MinimumSize = new Point(48, 48);
        _titlebar.CloseButton.Size = new Point(48, 48);
        _titlebar.Label.SetBounds(8, 0, Math.Max(1, Width - 152), 48);
        _fullscreenClose!.SetBounds(Width - 136, 0, 132, 48);
        _fullscreenClose.Show();
        _fullscreenClose.BringToFront();
        viewport.SetBounds(0, 48, Width, Math.Max(1, Height - 48));
        var content = InnerPanel;
        var width = Math.Max(_fullscreenContentSize.X, content.Children.Where(c => c.IsVisibleInParent).Select(c => c.Right).DefaultIfEmpty(0).Max());
        var height = Math.Max(_fullscreenContentSize.Y, content.Children.Where(c => c.IsVisibleInParent).Select(c => c.Bottom).DefaultIfEmpty(0).Max());
        content.SetBounds(Math.Max(0, (viewport.Width - width - 24) / 2), 0, width, height);
        viewport.VerticalScrollBar.Width = 24;
        viewport.HorizontalScrollBar.Height = 24;
        viewport.UpdateScrollBars();
    }

    private sealed class FullscreenCloseButton(Base parent) : Button(parent, "MobileFullscreenClose")
    {
        protected override void Render(Skin.Base skin)
        {
            var previous = skin.Renderer.DrawColor;
            skin.Renderer.DrawColor = new Color(255, 40, 49, 61);
            skin.Renderer.DrawFilledRect(new Rectangle(0, 0, Width, Height));
            skin.Renderer.DrawColor = new Color(255, 188, 151, 76);
            skin.Renderer.DrawFilledRect(new Rectangle(0, 0, Width, 2));
            skin.Renderer.DrawColor = previous;
            var background = ShouldDrawBackground;
            ShouldDrawBackground = false;
            base.Render(skin);
            ShouldDrawBackground = background;
        }
    }
}
