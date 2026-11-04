using System.Globalization;
using Intersect.Client.General;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.ControlInternal;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Exportation;
using Intersect.Framework.Core.GameObjects.Items;
using Rect = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Asset-backed Exportation crate preparation surface. All mutations are
/// requested through callbacks; only the server may change ExportationRun.
/// This is not automatically enabled until the server packet handler is wired.
/// </summary>
internal sealed class ExportationWindow : Base
{
    private const int DesignWidth = 1672, DesignHeight = 941, ShelfSize = 5, OrderSize = 3;
    private const string Background = "exportation/background.png";
    private const string SpriteDirectory = "exportation/sprites/";
    private static readonly Color Cream = new(255, 250, 233, 197);
    private static readonly Color DarkInk = new(255, 66, 33, 16);
    private static readonly Color Gold = new(255, 247, 199, 100);
    private static readonly Color Green = new(255, 132, 231, 107);

    private readonly Canvas _canvas;
    private readonly Action<Guid, CargoGroup, int> _packRequested;
    private readonly Action _sealRequested;
    private readonly Action _deliverRequested;
    private readonly Action _restartRequested;
    private readonly ExportationAudio _audio = new();
    private readonly Dictionary<string, IGameTexture?> _textures = new(StringComparer.Ordinal);
    private readonly List<(Base Control, int X, int Y, int Width, int Height, int Font)> _placements = [];
    private readonly Label[] _orderLabels = new Label[OrderSize];
    private readonly Label[] _shelfLabels = new Label[ShelfSize];
    private readonly Button[] _shelfButtons = new Button[ShelfSize];
    private readonly Label _merchant, _recipient, _routeName, _boat, _reward, _experience;
    private readonly Label _level, _progress, _warning, _sourceMap, _destinationMap, _status, _shelfPageLabel, _orderPageLabel;
    private readonly Button _seal, _nextGoods, _previousGoods, _nextOrder, _previousOrder;
    private readonly List<(Base Control, string Source)> _translated = [];
    private ExportationRoute? _route;
    private ExportationRun? _run;
    private int _goodsPage, _orderPage, _professionLevel = 1;
    private string _merchantName = "", _recipientName = "", _sourceMapName = "", _destinationMapName = "";
    private string _language = "";
    private bool _boatReady, _destroyed;
    private double _scale;
    private int _left, _top;

    internal bool ExitRequested { get; private set; }

    private static string T(string source) => ExportationText.Get(source, ClientLanguages.Current);
    private static string F(string source, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(source), args);

    internal ExportationWindow(Canvas canvas, Action<Guid, CargoGroup, int> packRequested,
        Action sealRequested, Action deliverRequested, Action restartRequested) : base(canvas, nameof(ExportationWindow))
    {
        _canvas = canvas;
        _packRequested = packRequested;
        _sealRequested = sealRequested;
        _deliverRequested = deliverRequested;
        _restartRequested = restartRequested;
        MouseInputEnabled = KeyboardInputEnabled = true;
        ShouldDrawBackground = false;
        Interface.InputBlockingComponents.Add(this);

        Title("Title", "Exportation", 582, 54, 575, 65, 42);
        Title("Subtitle", "Cargo preparation", 613, 111, 470, 40, 25);
        _level = Title("Profession", "Level {0}", 1247, 80, 169, 34, 21);
        Title("OrderTitle", "ORDER SLIP", 144, 193, 323, 47, 28, dark: true);
        Title("CrateTitle", "PACK THE CRATES", 549, 193, 494, 43, 28);
        Title("DeliveryTitle", "DELIVERY", 1144, 193, 331, 48, 27);
        Title("Instruction", "Click goods to pack a matching crate", 549, 237, 495, 31, 17);
        Title("Provided", "Goods provided by the merchant", 156, 684, 310, 38, 18, dark: true);
        Title("AtDelivery", "At delivery", 1158, 535, 287, 33, 19);

        _merchant = Title("Merchant", "", 257, 304, 205, 67, 22, dark: true);
        _sourceMap = Title("Source", "", 1136, 245, 170, 39, 19, dark: true);
        _destinationMap = Title("Destination", "", 1340, 357, 177, 39, 18, dark: true);
        _recipient = Title("Recipient", "", 1210, 418, 284, 58, 18);
        _boat = Title("Boat", "", 1200, 483, 249, 35, 19);
        _reward = Title("Reward", "", 1218, 573, 270, 36, 22);
        _experience = Title("Experience", "", 1218, 609, 270, 34, 19);
        Title("AfterSeal", "After sealing", 1196, 685, 253, 30, 18, dark: true);
        _status = Title("Status", "", 1173, 717, 317, 45, 17, dark: true);
        _routeName = Title("Route", "", 275, 364, 191, 29, 15, dark: true);
        _progress = Title("Progress", "", 618, 581, 385, 31, 22);
        _warning = Title("Warning", "Death or sinking destroys the cargo", 435, 820, 572, 42, 20);
        _shelfPageLabel = Title("ShelfPage", "", 850, 766, 80, 30, 16);
        _orderPageLabel = Title("OrderPage", "", 192, 636, 155, 30, 16, dark: true);

        for (var i = 0; i < OrderSize; ++i)
        {
            var index = i;
            _orderLabels[i] = Title("OrderLine" + i, "", 223, 402 + i * 82, 246, 51, 19, dark: true);
            Button("Remove" + i, "-", 145, 406 + i * 82, 48, 46, 19, () => RequestRemove(index));
        }
        for (var i = 0; i < ShelfSize; ++i)
        {
            var index = i;
            _shelfButtons[i] = Button("Good" + i, "", 505 + i * 115, 652, 108, 95, 18, () => RequestPack(index));
            _shelfLabels[i] = Title("GoodLabel" + i, "", 509 + i * 115, 744, 107, 35, 14);
        }

        _seal = Button("Seal", "SEAL THE CARGO", 1096, 808, 397, 58, 23, RequestSeal);
        Button("Close", "×", 1479, 35, 63, 59, 33, () => ExitRequested = true);
        Button("Exit", "Close", 143, 812, 158, 57, 22, () => ExitRequested = true);
        _previousGoods = Button("PreviousGoods", "‹", 503, 765, 45, 33, 24, () => ChangeGoodsPage(-1));
        _nextGoods = Button("NextGoods", "›", 1033, 765, 45, 33, 24, () => ChangeGoodsPage(1));
        _previousOrder = Button("PreviousOrders", "‹", 137, 642, 40, 27, 20, () => ChangeOrderPage(-1));
        _nextOrder = Button("NextOrders", "›", 406, 642, 40, 27, 20, () => ChangeOrderPage(1));
        UpdateLayout();
        _audio.Play("ui_open");
        BringToFront();
        Focus();
    }

    internal void Apply(ExportationRoute route, ExportationRun run,
        string merchant, string recipient, string sourceMap, string destinationMap,
        bool boatReady, int professionLevel)
    {
        if (route is not { IsValid: true } || run.RouteId != route.Id)
            return;
        var priorStage = _run?.Stage;
        var before = _run?.Packed?.ToDictionary(p => p.Key, p => p.Value) ?? [];
        _route = route;
        _run = run;
        _merchantName = merchant;
        _recipientName = recipient;
        _sourceMapName = sourceMap;
        _destinationMapName = destinationMap;
        _boatReady = boatReady;
        _professionLevel = Math.Max(1, professionLevel);

        if (priorStage.HasValue && priorStage != run.Stage)
        {
            _audio.Play(run.Stage switch
            {
                DeliveryStage.InTransit => "cargo_seal",
                DeliveryStage.Lost => "cargo_lost",
                DeliveryStage.Delivered => "delivery_success",
                _ => "ui_open",
            });
        }
        else if (priorStage == DeliveryStage.Preparing && run.Stage == DeliveryStage.Preparing)
        {
            foreach (var line in route.Lines)
            {
                var delta = ExportationRules.Packed(run, line.ItemId) - before.GetValueOrDefault(line.ItemId);
                if (delta == 0) continue;
                _audio.Play(delta < 0 ? "item_remove" : line.Group == CargoGroup.Fragile ? "item_glass" :
                    line.Group == CargoGroup.Materials ? "item_wood" : "item_metal");
                if (ExportationRules.ReadyToSeal(route, run)) _audio.Play("order_complete");
                break;
            }
        }
        RefreshLabels();
        Show();
        BringToFront();
    }

    internal void Update()
    {
        UpdateLayout();
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (_route is not { } route || _run is not { } run) return;
        _level.Text = F("Level {0}", _professionLevel);
        _merchant.Text = ClientLanguages.ContentText(_merchantName);
        _recipient.Text = F("Recipient: {0}", ClientLanguages.ContentText(_recipientName));
        _routeName.Text = ClientLanguages.ContentText(route.Name);
        _sourceMap.Text = ClientLanguages.ContentText(_sourceMapName);
        _destinationMap.Text = ClientLanguages.ContentText(_destinationMapName);
        _boat.Text = T(_boatReady ? "Boat ready" : "Boat required");
        _boat.TextColorOverride = _boatReady ? Green : new Color(255, 255, 155, 138);
        _reward.Text = route.RewardQuantity > 0 ?
            $"{route.RewardQuantity:N0} {ItemName(route.RewardItemId)}" : "";
        _experience.Text = F("+{0:N0} Exportation XP", route.ExperienceReward);
        var total = route.Lines.Sum(line => line.Quantity);
        var packed = route.Lines.Sum(line => ExportationRules.Packed(run, line.ItemId));
        _progress.Text = F("{0} / {1} items packed", packed, total);
        _status.Text = T(run.Stage switch
        {
            DeliveryStage.Preparing => "Complete the order",
            DeliveryStage.InTransit => "Delivery quest started",
            DeliveryStage.Lost => "Cargo lost — return to the merchant",
            DeliveryStage.Delivered => "Delivery successful",
            _ => "Cargo preparation",
        });
        _warning.Text = T("Death or sinking destroys the cargo");
        var onRecipientIsland = Globals.Me?.MapId == route.RecipientMapId;
        var onSourceIsland = Globals.Me?.MapId == route.SourceMapId;
        _seal.Text = T(run.Stage switch
        {
            DeliveryStage.InTransit => "DELIVER CARGO",
            DeliveryStage.Lost => "PREPARE CARGO AGAIN",
            _ => "SEAL THE CARGO",
        });
        _seal.IsHidden = run.Stage is DeliveryStage.Delivered or DeliveryStage.Abandoned;
        _seal.IsDisabled = run.Stage switch
        {
            DeliveryStage.Preparing => !_boatReady || !ExportationRules.ReadyToSeal(route, run),
            DeliveryStage.InTransit => !onRecipientIsland || !run.ArrivedByShip,
            DeliveryStage.Lost => !onSourceIsland || !_boatReady,
            _ => true,
        };
        _orderPage = Math.Min(_orderPage, Math.Max(0, (route.Lines.Length - 1) / OrderSize));
        _goodsPage = Math.Min(_goodsPage, Math.Max(0, (route.Lines.Length - 1) / ShelfSize));
        _previousOrder.IsDisabled = _orderPage == 0;
        _nextOrder.IsDisabled = (_orderPage + 1) * OrderSize >= route.Lines.Length;
        _previousGoods.IsDisabled = _goodsPage == 0;
        _nextGoods.IsDisabled = (_goodsPage + 1) * ShelfSize >= route.Lines.Length;
        _orderPageLabel.Text = $"{_orderPage + 1} / {(route.Lines.Length + OrderSize - 1) / OrderSize}";
        _shelfPageLabel.Text = $"{_goodsPage + 1} / {(route.Lines.Length + ShelfSize - 1) / ShelfSize}";
        for (var i = 0; i < OrderSize; ++i)
        {
            var line = route.Lines.ElementAtOrDefault(_orderPage * OrderSize + i);
            _orderLabels[i].Text = line is null ? "" :
                $"{ItemName(line.ItemId)}\n{ExportationRules.Packed(run, line.ItemId)} / {line.Quantity}";
        }
        for (var i = 0; i < ShelfSize; ++i)
        {
            var line = route.Lines.ElementAtOrDefault(_goodsPage * ShelfSize + i);
            _shelfLabels[i].Text = line is null ? "" : ItemName(line.ItemId);
            _shelfButtons[i].IsDisabled = line is null ||
                run.Stage != DeliveryStage.Preparing ||
                ExportationRules.Packed(run, line.ItemId) >= line.Quantity;
        }
    }

    private void RequestPack(int i)
    {
        if (_route is not { } r || _run is not { Stage: DeliveryStage.Preparing } s) return;
        var line = r.Lines.ElementAtOrDefault(_goodsPage * ShelfSize + i);
        if (line is null || ExportationRules.Packed(s, line.ItemId) >= line.Quantity) return;
        _packRequested(line.ItemId, line.Group, 1);
    }

    private void RequestRemove(int i)
    {
        if (_route is not { } r || _run is not { Stage: DeliveryStage.Preparing } s) return;
        var line = r.Lines.ElementAtOrDefault(_orderPage * OrderSize + i);
        if (line is null || ExportationRules.Packed(s, line.ItemId) <= 0) return;
        _packRequested(line.ItemId, line.Group, -1);
    }

    private void RequestSeal()
    {
        if (_route is not { } r || _run is not { } s || _seal.IsDisabled) return;
        switch (s.Stage)
        {
            case DeliveryStage.Preparing when _boatReady && ExportationRules.ReadyToSeal(r, s):
                _sealRequested();
                break;
            case DeliveryStage.InTransit when s.ArrivedByShip && Globals.Me?.MapId == r.RecipientMapId:
                _deliverRequested();
                break;
            case DeliveryStage.Lost when _boatReady && Globals.Me?.MapId == r.SourceMapId:
                _restartRequested();
                break;
        }
    }
    private void ChangeGoodsPage(int direction) { _goodsPage = Math.Max(0, _goodsPage + direction); }
    private void ChangeOrderPage(int direction) { _orderPage = Math.Max(0, _orderPage + direction); }

    private static string ItemName(Guid id) =>
        ItemDescriptor.TryGet(id, out var item) ? ClientLanguages.ContentText(item.Name) : id.ToString("N")[..8];

    protected override void Render(SkinBase skin)
    {
        skin.Renderer.DrawColor = new Color(180, 15, 12, 9);
        skin.Renderer.DrawFilledRect(new Rect(0, 0, Width, Height));
        DrawTexture(skin, Background, 0, 0, DesignWidth, DesignHeight);
        if (_route is { } r && _run is { } s)
        {
            DrawTexture(skin, SpriteDirectory + "marchand.png", 146, 282, 96, 95);
            DrawTexture(skin, SpriteDirectory + "destinataire.png", 1128, 427, 62, 63);
            DrawTexture(skin, SpriteDirectory + "bateau.png", 1182, 476, 58, 50);
            DrawTexture(skin, SpriteDirectory + "parchemin_quete.png", 1128, 685, 61, 57);
            DrawTexture(skin, SpriteDirectory + "sceau_royal.png", 304, 719, 51, 58);
            for (var i = 0; i < OrderSize; ++i)
            {
                var line = r.Lines.ElementAtOrDefault(_orderPage * OrderSize + i);
                if (line is not null) DrawGood(skin, line, 147, 397 + i * 82, 74, 65);
            }
            for (var i = 0; i < ShelfSize; ++i)
            {
                var line = r.Lines.ElementAtOrDefault(_goodsPage * ShelfSize + i);
                if (line is not null) DrawGood(skin, line, 522 + i * 115, 651, 79, 74);
            }
            var materials = r.Lines.Where(line => line.Group == CargoGroup.Materials && ExportationRules.Packed(s, line.ItemId) > 0).Take(3).ToArray();
            var fragile = r.Lines.Where(line => line.Group != CargoGroup.Materials && ExportationRules.Packed(s, line.ItemId) > 0).Take(4).ToArray();
            for (var i = 0; i < materials.Length; ++i)
                DrawGood(skin, materials[i], 543 + i % 2 * 95, 315 + i / 2 * 105, 105, 85);
            for (var i = 0; i < fragile.Length; ++i)
                DrawGood(skin, fragile[i], 842 + i % 2 * 96, 306 + i / 2 * 100, 85, 89);
            var total = r.Lines.Sum(l => l.Quantity);
            var completed = r.Lines.Sum(l => ExportationRules.Packed(s, l.ItemId));
            skin.Renderer.DrawColor = Green;
            var bar = SceneRect(582, 615, (int)(407d * completed / Math.Max(1, total)), 14);
            if (bar.Width > 0) skin.Renderer.DrawFilledRect(bar);
            if (s.Stage == DeliveryStage.InTransit) DrawTexture(skin, SpriteDirectory + "caisse_scellee.png", 783, 381, 118, 116);
        }
        base.Render(skin);
    }

    private void DrawGood(SkinBase skin, CargoLine line, int x, int y, int w, int h)
    {
        if (!string.IsNullOrEmpty(line.Sprite) && ExportationArt.IsGoodsSprite(line.Sprite))
        {
            DrawTexture(skin, SpriteDirectory + line.Sprite + ".png", x, y, w, h);
            return;
        }
        if (ItemDescriptor.TryGet(line.ItemId, out var item))
        {
            var texture = GameContentManager.Current.GetTexture(TextureType.Item, item.Icon);
            if (texture is not null)
                skin.Renderer.DrawTexturedRect(texture, SceneRect(x, y, w, h), Color.White);
        }
    }

    private void DrawTexture(SkinBase skin, string name, int x, int y, int w, int h)
    {
        if (!_textures.TryGetValue(name, out var image))
            _textures[name] = image = GameContentManager.Current.GetTexture(TextureType.Gui, name);
        if (image is not null) skin.Renderer.DrawTexturedRect(image, SceneRect(x, y, w, h), Color.White);
    }

    private Rect SceneRect(int x, int y, int width, int height) =>
        new(_left + (int)Math.Round(x * _scale), _top + (int)Math.Round(y * _scale),
            Math.Max(1, (int)Math.Round(width * _scale)), Math.Max(1, (int)Math.Round(height * _scale)));

    private Label Title(string name, string source, int x, int y, int w, int h, int font, bool dark = false)
    {
        var label = new Label(this, "Exportation" + name)
        {
            Text = T(source), Font = Skin.DefaultFont, FontSize = font,
            AutoSizeToContents = false, WrappingBehavior = WrappingBehavior.Wrapped,
            TextColorOverride = dark ? DarkInk : Cream, TextAlign = Pos.Left | Pos.CenterV,
            MouseInputEnabled = false, KeyboardInputEnabled = false,
        };
        Place(label, x, y, w, h, font);
        if (source.Length > 0) _translated.Add((label, source));
        return label;
    }

    private Button Button(string name, string source, int x, int y, int w, int h, int font, Action click)
    {
        var button = new Button(this, "Exportation" + name)
        {
            Text = T(source), Font = Skin.DefaultFont, FontSize = font, TextColorOverride = Gold,
            ShouldDrawBackground = false,
        };
        foreach (var state in Enum.GetValues<ButtonSoundState>()) button.SetSound(state, null);
        button.Clicked += (_, _) => { click(); Focus(); };
        Place(button, x, y, w, h, font);
        if (source.Length > 0) _translated.Add((button, source));
        return button;
    }

    private void Place(Base control, int x, int y, int w, int h, int font) =>
        _placements.Add((control, x, y, w, h, font));

    private void UpdateLayout()
    {
        SetPosition(0, 0); SetSize(_canvas.Width, _canvas.Height);
        _scale = Math.Min(Math.Max(1, _canvas.Width) / (double)DesignWidth,
            Math.Max(1, _canvas.Height) / (double)DesignHeight);
        _left = (int)((_canvas.Width - DesignWidth * _scale) / 2);
        _top = (int)((_canvas.Height - DesignHeight * _scale) / 2);
        foreach (var (control, x, y, w, h, font) in _placements)
        {
            var rect = SceneRect(x, y, w, h);
            control.SetBounds(rect.X, rect.Y, rect.Width, rect.Height);
            if (control is Label label) label.FontSize = Math.Max(8, (int)Math.Round(font * _scale));
            if (control is Button button) button.FontSize = Math.Max(8, (int)Math.Round(font * _scale));
        }
        if (_language == ClientLanguages.Current) return;
        _language = ClientLanguages.Current;
        foreach (var (control, source) in _translated)
        {
            if (control is Label label) label.Text = T(source);
            else if (control is Button button) button.Text = T(source);
        }
    }

    internal void ReleaseForCanvasDispose()
    {
        Interface.InputBlockingComponents.Remove(this);
    }

    internal void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;
        _audio.Play("ui_close");
        Interface.InputBlockingComponents.Remove(this);
        Hide();
        Parent?.RemoveChild(this, false);
        Dispose();
    }
}
