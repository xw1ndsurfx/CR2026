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
    private readonly PreviewShadow _shadow;
    private readonly Button _zoomOut;
    private readonly Button _zoomIn;
    private readonly Label _zoomLabel;
    private int _zoom = 4;
    private int _direction;
    private IGameTexture? _bodyTexture;
    private int _animationFrame = -1;
    private readonly Dictionary<ImagePanel,(int Width,int Height)> _layers=[];
    public bool Animated {get;set;}=true;
    private int _bodyWidth = 32, _bodyHeight = 48;
    private int _visibleWidth=32,_visibleHeight=48;
    private float _centerOffset, _bottomPadding;
    public event Action? ZoomChanged;
    public int Zoom => _zoom;
    public int FeetY => Height * 2 / 3;
    private int MaximumZoom=>Math.Clamp((int)Math.Ceiling(Math.Min((Width-20f)/_visibleWidth,(FeetY-16f)/_visibleHeight)),1,8);
    public float PreviewScale => Math.Min(_zoom, Math.Min((Width - 20f) / _visibleWidth, (FeetY - 16f) / _visibleHeight));

    public AppearancePreview(Base parent, string name) : base(parent, name)
    {
        _floor = new ImagePanel(this, "PreviewFloor")
        {
            TextureFilename = "character_preview_background.png", MouseInputEnabled = false,
            MaintainAspectRatio = false,
        };
        _shadow=new PreviewShadow(this);
        _zoomOut = new Button(this, "PreviewZoomOut") { Text = "-", FontSize = 12 };
        _zoomIn = new Button(this, "PreviewZoomIn") { Text = "+", FontSize = 12 };
        _zoomLabel = new Label(this, "PreviewZoom") { FontSize = 10, TextAlign = Pos.Center,AutoSizeToContents=false,MouseInputEnabled=false };
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
        var zoom = Math.Clamp(_zoom + delta, 1, MaximumZoom);
        if (zoom == _zoom) return;
        _zoom = zoom; Arrange(); ZoomChanged?.Invoke();
    }

    public void SetBodySize(int width, int height)
    {
        _bodyTexture = null;
        _animationFrame = -1;
        _bodyWidth = Math.Max(1, width); _bodyHeight = Math.Max(1, height);
        _visibleWidth=_bodyWidth;_visibleHeight=_bodyHeight;
        _centerOffset = _bottomPadding = 0; Arrange();
    }

    public void SetBodyFrame(IGameTexture texture, int width, int height, int direction)
    {
        _direction=direction;
        SetBodySize(width, height);
        _bodyTexture = texture;
        // Keep the zoom limit stable while frames animate, even when their padding differs.
        var frames = Math.Max(1, texture.Width / _bodyWidth);
        var visibleWidth = 0;
        var visibleHeight = 0;
        for (var frame = 0; frame < frames; frame++)
        {
            var visible = SpriteVisibleBounds.Get(texture,
                new FloatRect(frame * _bodyWidth, direction * _bodyHeight, _bodyWidth, _bodyHeight));
            if (visible.IsEmpty) continue;
            visibleWidth = Math.Max(visibleWidth, visible.Width);
            visibleHeight = Math.Max(visibleHeight, visible.Height);
        }
        if (visibleWidth > 0 && visibleHeight > 0)
        {
            _visibleWidth = visibleWidth;
            _visibleHeight = visibleHeight;
        }
        UpdateAnimationFrame(0);
    }

    private void UpdateAnimationFrame(int frameIndex)
    {
        if (_animationFrame == frameIndex) return;
        _animationFrame = frameIndex;
        if (_bodyTexture is { } body)
        {
            var count = Math.Max(1, body.Width / _bodyWidth);
            var visible = SpriteVisibleBounds.Get(body,
                new FloatRect(frameIndex % count * _bodyWidth, _direction * _bodyHeight, _bodyWidth, _bodyHeight));
            if (!visible.IsEmpty)
            {
                _centerOffset = visible.X + visible.Width / 2f - _bodyWidth / 2f;
                _bottomPadding = _bodyHeight - visible.Y - visible.Height;
            }
        }
        foreach (var (layer, frame) in _layers)
        {
            if (!layer.IsVisibleInParent || layer.Texture is not { } texture) continue;
            var count = Math.Max(1, texture.Width / Math.Max(1, frame.Width));
            layer.SetTextureRect(frameIndex % count * frame.Width, _direction * frame.Height, frame.Width, frame.Height);
        }
        Arrange();
    }

    public void PlaceLayer(ImagePanel layer, int width, int height)
    {
        if (!_layers.ContainsKey(layer)) layer.Disposed += (_, _) => _layers.Remove(layer);
        if (!_layers.TryGetValue(layer, out var previous) || previous != (width, height))
            _layers[layer] = (width, height);
        layer.MouseInputEnabled=false;layer.KeyboardInputEnabled=false;layer.ShouldDrawBackground=false;
        var scale = Math.Max(0.1f, PreviewScale);
        var w = (int)Math.Round(width * scale); var h = (int)Math.Round(height * scale);
        layer.Alignment = [];
        layer.Dock = Pos.None;
        layer.RestrictToParent = false;
        layer.MaintainAspectRatio = false;
        layer.SetBounds((int)Math.Round((Width - w) / 2f - _centerOffset * scale),
            (int)Math.Round(FeetY - h + _bottomPadding * scale), w, h);
        _zoomOut.BringToFront();_zoomIn.BringToFront();_zoomLabel.BringToFront();
    }

    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        var count = Math.Max(1, (_bodyTexture?.Width ?? _bodyWidth) / _bodyWidth);
        var index = Animated ? (int)(Environment.TickCount64 / 140 % count) : 0;
        UpdateAnimationFrame(index);
        base.Render(skin);
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
        _shadow.SetBounds((Width-width/2)/2,FeetY-5,width/2,10);
        _floor.SendToBack();
        foreach (var (layer, frame) in _layers)
        {
            PlaceLayer(layer, frame.Width, frame.Height);
        }
        _zoomOut.SetBounds((Width - 152) / 2, Height - 36, 36, 28);
        _zoomLabel.SetBounds((Width - 152) / 2 + 38, Height - 36, 76, 28);
        _zoomIn.SetBounds((Width - 152) / 2 + 116, Height - 36, 36, 28);
        _zoomLabel.Text = ClientLanguages.Text("Zoom") + " x" + _zoom;
        _zoomOut.IsDisabled = _zoom == 1; _zoomIn.IsDisabled = _zoom >= MaximumZoom;
        _zoomOut.BringToFront();_zoomIn.BringToFront();_zoomLabel.BringToFront();
    }
    private sealed class PreviewShadow:Base
    {
        internal PreviewShadow(Base parent):base(parent,"PreviewShadow")
        {MouseInputEnabled=false;KeyboardInputEnabled=false;}
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            skin.Renderer.DrawColor=new Color(110,0,0,0);
            for(var row=0;row<Height;row++)
            {
                var radius=Width*.5f*MathF.Sqrt(Math.Max(0,1-MathF.Pow((row-Height*.5f)/(Height*.5f),2)));
                skin.Renderer.DrawFilledRect(new Rectangle((int)(Width*.5f-radius),row,(int)(radius*2),1));
            }
        }
    }

}
