using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace Intersect.Client.Interface.Shared;

/// <summary>A single foot anchor for the floor and every paperdoll layer, independent of world scale.</summary>
internal sealed class AppearancePreview : ImagePanel
{
    private readonly ImagePanel _floor;
    private readonly Button _zoomOut;
    private readonly Button _zoomIn;
    private readonly Label _zoomLabel;
    private int _zoom = 4;
    private int _bodyWidth = 32, _bodyHeight = 48;
    private float _centerOffset, _bottomPadding;
    public event Action? ZoomChanged;
    public int Zoom => _zoom;
    public int FeetY => Height * 2 / 3;
    public float PreviewScale => Math.Min(_zoom, Math.Min((Width - 20f) / _bodyWidth, (FeetY - 16f) / _bodyHeight));

    public AppearancePreview(Base parent, string name) : base(parent, name)
    {
        _floor = new ImagePanel(this, "PreviewFloor")
        {
            TextureFilename = "character_preview_background.png", MouseInputEnabled = false,
            MaintainAspectRatio = false,
        };
        _zoomOut = new Button(this, "PreviewZoomOut") { Text = "-", FontSize = 12 };
        _zoomIn = new Button(this, "PreviewZoomIn") { Text = "+", FontSize = 12 };
        _zoomLabel = new Label(this, "PreviewZoom") { FontSize = 10, TextAlign = Pos.Center };
        _zoomOut.Clicked += (_, _) => ChangeZoom(-1);
        _zoomIn.Clicked += (_, _) => ChangeZoom(1);
        _zoomOut.SetToolTipText(ClientLanguages.Text("Zoom out"));
        _zoomIn.SetToolTipText(ClientLanguages.Text("Zoom in"));
        SetToolTipText(ClientLanguages.Text("Scroll to zoom"));
        Arrange();
    }

    public override void LoadJson(JToken token, bool isRoot = false)
    {
        base.LoadJson(token, isRoot);
        // Old layouts used this control as the floor image; only the dedicated child draws it now.
        Texture = null; MaintainAspectRatio = false; Alignment = []; Arrange();
    }

    public void ChangeZoom(int delta)
    {
        var zoom = Math.Clamp(_zoom + delta, 1, 8);
        if (zoom == _zoom) return;
        _zoom = zoom; Arrange(); ZoomChanged?.Invoke();
    }

    public void SetBodySize(int width, int height)
    {
        _bodyWidth = Math.Max(1, width); _bodyHeight = Math.Max(1, height);
        _centerOffset = _bottomPadding = 0; Arrange();
    }

    public void SetBodyFrame(IGameTexture texture, int width, int height, int direction)
    {
        SetBodySize(width, height);
        var visible = SpriteVisibleBounds.Get(texture, new FloatRect(0, direction * height, width, height));
        if (visible.IsEmpty) return;
        _centerOffset = visible.X + visible.Width / 2f - width / 2f;
        _bottomPadding = height - visible.Y - visible.Height;
    }

    public void PlaceLayer(ImagePanel layer, int width, int height)
    {
        var scale = Math.Max(0.1f, PreviewScale);
        var w = (int)Math.Round(width * scale); var h = (int)Math.Round(height * scale);
        layer.Alignment = [];
        layer.SetBounds((int)Math.Round((Width - w) / 2f - _centerOffset * scale),
            (int)Math.Round(FeetY - h + _bottomPadding * scale), w, h);
    }

    protected override void OnSizeChanged(Point oldSize, Point newSize)
    {
        base.OnSizeChanged(oldSize, newSize);
        if (_floor != null) Arrange();
    }

    protected override bool OnMouseWheeled(int delta)
    {
        if (delta == 0) return false;
        ChangeZoom(delta > 0 ? 1 : -1); return true;
    }

    private void Arrange()
    {
        var width = Math.Clamp((int)(48 * Math.Max(0.1f, PreviewScale)), 64, Math.Max(64, Width - 16));
        var height = width / 2;
        _floor.SetBounds((Width - width) / 2, FeetY - height / 2, width, height);
        _zoomOut.SetBounds((Width - 152) / 2, Height - 36, 36, 28);
        _zoomLabel.SetBounds((Width - 152) / 2 + 38, Height - 36, 76, 28);
        _zoomIn.SetBounds((Width - 152) / 2 + 116, Height - 36, 36, 28);
        _zoomLabel.Text = ClientLanguages.Text("Zoom") + " x" + _zoom;
        _zoomOut.IsDisabled = _zoom == 1; _zoomIn.IsDisabled = _zoom == 8;
    }
}
