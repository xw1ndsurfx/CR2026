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
    // The original art has transparent panels. At night, a 70%-opaque black
    // screen dimmer made the world show through as almost black, and the dark
    // parchment ink became unreadable. Give the UI its own warm, opaque
    // backplate; dim only the outside world, never the minigame itself.
    private const byte WorldDimOpacity = 82;
    private static readonly Color WoodFrame = new(255, 64, 44, 31);
    private static readonly Color ParchmentBacking = new(255, 203, 170, 124);
    private static readonly Color CrateBacking = new(255, 99, 70, 48);
    private static readonly Color DeliveryBacking = new(255, 100, 73, 51);
    private static readonly Color WarmParchmentWash = new(78, 255, 226, 181);
    private static readonly Color WarmWoodWash = new(12, 224, 173, 113);
    private static readonly Color Cream = new(255, 255, 246, 220);
    private static readonly Color DarkInk = new(255, 69, 41, 23);
    private static readonly Color Gold = new(255, 255, 222, 134);
    private static readonly Color Green = new(255, 142, 236, 107);
    private static readonly Color SuccessGreen = new(255, 42, 126, 56);
    private static readonly Color Bronze = new(255, 207, 156, 82);
    private static readonly Color Ember = new(255, 190, 77, 47);

    private readonly Canvas _canvas;
    private readonly Action<Guid, CargoGroup, int> _packRequested;
    private readonly Action _sealRequested;
    private readonly Action _deliverRequested;
    private readonly Action _restartRequested;
    private readonly ExportationAudio _audio = new();
    private readonly Dictionary<string, IGameTexture?> _textures = new(StringComparer.Ordinal);
    private readonly List<(Base Control, int X, int Y, int Width, int Height, int Font)> _placements = [];
    private readonly Dictionary<Label, float> _textScaleCache = [];
    private readonly Label[] _orderLabels = new Label[OrderSize];
    private readonly Label[] _shelfLabels = new Label[ShelfSize];
    private readonly Label[] _journeySteps = new Label[3];
    private readonly Label[] _helpLines = new Label[6];
    private readonly Button[] _shelfButtons = new Button[ShelfSize];
    private readonly Button[] _removeButtons = new Button[OrderSize];
    private readonly Label _merchant, _recipient, _routeName, _boat, _reward, _experience;
    private readonly Label _level, _professionXp, _progress, _warning, _sourceMap, _destinationMap, _status, _shelfPageLabel, _orderPageLabel;
    private readonly Label _leftCrate, _rightCrate;
    private readonly Button _seal, _nextGoods, _previousGoods, _nextOrder, _previousOrder, _helpButton;
    private readonly List<(Base Control, string Source)> _translated = [];
    private ExportationRoute? _route;
    private ExportationRun? _run;
    private int _goodsPage, _orderPage, _professionLevel = 1;
    private long _experienceInLevel, _experienceRequired;
    private bool _maximumLevel;
    private string _merchantName = "", _recipientName = "", _sourceMapName = "", _destinationMapName = "";
    private string _language = "";
    private bool _boatReady, _destroyed, _helpOpen;
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

        // Coordinates are authored for the approved 1672×941 GFX, not the
        // generated preview. Text must sit inside the original plaque artwork.
        Title("Title", "Exportation", 586, 69, 506, 54, 40, center: true);
        Title("Subtitle", "Cargo preparation", 632, 126, 410, 31, 24, center: true);
        _level = Title("Profession", "Level {0}", 1245, 78, 210, 33, 22);
        _professionXp = Title("ProfessionXp", "", 1245, 112, 252, 31, 16);

        Title("OrderTitle", "ORDER SLIP", 143, 199, 322, 45, 28, dark: true, center: true);
        Title("CrateTitle", "PACK THE CRATES", 510, 200, 532, 43, 28, center: true);
        Title("DeliveryTitle", "DELIVERY", 1099, 200, 395, 44, 27, center: true);
        Title("Instruction", "Click the goods below to fill the crates",
            526, 249, 526, 31, 19, center: true);
        Title("Provided", "Prepare the order, seal it, then sail to the recipient.",
            150, 685, 310, 48, 16, dark: true, center: true, wrap: true);
        Title("AtDelivery", "At delivery", 1155, 543, 300, 34, 20, center: true);

        // Preserve the portrait, parchment and route art, while adding the
        // missing headings from the original concept in their own empty areas.
        Title("MerchantCaption", "MERCHANT", 252, 280, 204, 23, 14, dark: true);
        _merchant = Title("Merchant", "", 252, 308, 215, 40, 22, dark: true, wrap: true);
        Title("DestinationCaption", "DESTINATION", 252, 350, 204, 22, 14, dark: true);
        _routeName = Title("Route", "", 252, 371, 215, 29, 17, dark: true, wrap: true);
        Title("GoodsCaption", "REQUESTED GOODS",
            144, 400, 319, 23, 17, dark: true, center: true);

        _sourceMap = Title("Source", "", 1117, 254, 181, 42, 17, center: true, wrap: true);
        _destinationMap = Title("Destination", "",
            1334, 349, 178, 52, 17, center: true, wrap: true);
        _recipient = Title("Recipient", "", 1211, 423, 284, 54, 19, wrap: true);
        _boat = Title("Boat", "", 1183, 485, 283, 35, 19, center: true);
        _reward = Title("Reward", "", 1196, 578, 302, 36, 21, center: true, wrap: true);
        _experience = Title("Experience", "",
            1200, 613, 298, 36, 19, center: true);
        _status = Title("Status", "", 1171, 686, 326, 27, 18, dark: true, center: true);
        _journeySteps[0] = Title("JourneyOne", "", 1164, 713, 336, 24, 15, dark: true);
        _journeySteps[1] = Title("JourneyTwo", "", 1164, 738, 336, 24, 15, dark: true);
        _journeySteps[2] = Title("JourneyThree", "", 1164, 762, 336, 24, 15, dark: true);
        _progress = Title("Progress", "", 590, 573, 386, 39, 22, center: true);
        _leftCrate = Title("MaterialsCrate", "", 552, 516, 205, 40, 18, center: true);
        _rightCrate = Title("OtherCrate", "", 798, 516, 232, 40, 18, center: true);
        _warning = Title("Warning", "Death or sinking destroys the cargo",
            407, 819, 633, 44, 20, center: true);
        _shelfPageLabel = Title("ShelfPage", "", 794, 760, 173, 32, 17, center: true);
        _orderPageLabel = Title("OrderPage", "", 196, 644, 182, 31, 16,
            dark: true, center: true);

        for (var i = 0; i < OrderSize; ++i)
        {
            var index = i;
            _orderLabels[i] = Title("OrderLine" + i, "", 229, 426 + i * 78,
                192, 52, 18, dark: true, wrap: true);
            // This bitmap font does not contain the Unicode minus glyph:
            // the old button rendered '?' even in empty order lines.
            _removeButtons[i] = Button("Remove" + i, "X", 425, 432 + i * 78, 34, 29, 17,
                () => RequestRemove(index));
            _removeButtons[i].TextColorOverride = new Color(255, 255, 199, 153);
            _removeButtons[i].IsHidden = true;
        }
        for (var i = 0; i < ShelfSize; ++i)
        {
            var index = i;
            _shelfButtons[i] = Button("Good" + i, "", 497 + i * 111,
                656, 105, 87, 18, () => RequestPack(index));
            _shelfLabels[i] = Title("GoodLabel" + i, "", 500 + i * 111,
                728, 104, 49, 15, center: true, wrap: true);
        }

        _seal = Button("Seal", "SEAL THE CARGO", 1106, 806, 391, 61, 23, RequestSeal);
        Button("Exit", "Close", 146, 812, 155, 55, 22, () => ExitRequested = true);
        _previousGoods = Button("PreviousGoods", "<", 506, 761, 45, 33,
            24, () => ChangeGoodsPage(-1));
        _nextGoods = Button("NextGoods", ">", 1010, 761, 45, 33,
            24, () => ChangeGoodsPage(1));
        _previousOrder = Button("PreviousOrders", "<", 143, 644, 36, 30,
            20, () => ChangeOrderPage(-1));
        _nextOrder = Button("NextOrders", ">", 407, 644, 38, 30,
            20, () => ChangeOrderPage(1));
        _helpButton = Button("Help", "?", 1435, 74, 70, 60, 32, ToggleHelp);
        // Help is a real, clickable in-game overlay, not a static image.
        // Created last so these labels render above all existing game labels.
        var helpText = new[]
        {
            "EXPORTATION GUIDE",
            "1. Own a ship that has not sunk; it can be docked elsewhere.",
            "2. Pack every requested item. Use X to remove one.",
            "3. Seal the order, board your ship, and sail to the other island.",
            "4. Dock and speak to the recipient to earn the item and profession XP.",
            "Death or shipwreck loses the cargo. Press ? to close this guide.",
        };
        for (var i = 0; i < _helpLines.Length; ++i)
        {
            _helpLines[i] = Title("HelpLine" + i, helpText[i], 532, 310 + i * 46,
                486, 44, i == 0 ? 29 : 19, center: i == 0, wrap: true);
            _helpLines[i].IsHidden = true;
        }
        UpdateLayout();
        _audio.Play("ui_open");
        BringToFront();
        Focus();
    }

    internal void Apply(ExportationRoute route, ExportationRun run,
        string merchant, string recipient, string sourceMap, string destinationMap,
        bool boatReady, int professionLevel, long expInto, long expRequired, bool maxLevel)
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
        _experienceInLevel = expInto;
        _experienceRequired = expRequired;
        _maximumLevel = maxLevel;

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
        // Text is refreshed first so its actual length participates in
        // native pixel-font fitting, including language changes.
        RefreshLabels();
        UpdateLayout();
    }

    private void RefreshLabels()
    {
        if (_route is not { } route || _run is not { } run) return;
        _level.Text = F("Level {0}", _professionLevel);
        _professionXp.Text = _maximumLevel ? T("MAX LEVEL") :
            F("XP: {0:N0} / {1:N0}", _experienceInLevel, _experienceRequired);
        _merchant.Text = ClientLanguages.ContentText(_merchantName);
        _recipient.Text = F("Recipient: {0}", ClientLanguages.ContentText(_recipientName));
        _routeName.Text = ClientLanguages.ContentText(route.Name);
        _sourceMap.Text = ClientLanguages.ContentText(_sourceMapName);
        _destinationMap.Text = ClientLanguages.ContentText(_destinationMapName);
        _boat.Text = T(_boatReady ? "Boat owned" : "Boat required");
        _boat.TextColorOverride = _boatReady ? Green : new Color(255, 255, 155, 138);
        _reward.Text = route.RewardQuantity > 0 ?
            $"{route.RewardQuantity:N0} {ItemName(route.RewardItemId)}" : "";
        _experience.Text = F("+{0:N0} Exportation XP", route.ExperienceReward);
        var total = route.Lines.Sum(line => line.Quantity);
        var packed = route.Lines.Sum(line => ExportationRules.Packed(run, line.ItemId));
        _progress.Text = F("{0} / {1} items packed", packed, total);
        var materialProgress = ExportationUiPresentation.Cargo(route, run, true);
        var otherProgress = ExportationUiPresentation.Cargo(route, run, false);
        _leftCrate.Text = F("{0}: {1}/{2}", T("Materials"), materialProgress.Packed, materialProgress.Required);
        _rightCrate.Text = F("{0}: {1}/{2}", T("Other goods"), otherProgress.Packed, otherProgress.Required);
        _leftCrate.TextColorOverride = materialProgress.Complete ? Green : Cream;
        _rightCrate.TextColorOverride = otherProgress.Complete ? Green : Cream;
        _status.Text = T(run.Stage switch
        {
            DeliveryStage.Preparing => "Complete the order",
            DeliveryStage.InTransit => "Delivery quest started",
            DeliveryStage.Lost => "Cargo lost — return to the merchant",
            DeliveryStage.Delivered => "Delivery successful",
            _ => "Cargo preparation",
        });
        _warning.Text = T("Death or sinking destroys the cargo");
        var steps = new[]
        {
            T("1. Prepare and seal the cargo"),
            T("2. Sail to the destination"),
            T("3. Deliver to the recipient"),
        };
        for (var i = 0; i < _journeySteps.Length; i++)
        {
            _journeySteps[i].Text = steps[i];
            _journeySteps[i].TextColorOverride = ExportationUiPresentation.Step(run, i) switch
            {
                ExportationUiPresentation.StepState.Complete => SuccessGreen,
                ExportationUiPresentation.StepState.Active => DarkInk,
                _ => new Color(255, 125, 94, 65),
            };
        }
        if (run.Stage == DeliveryStage.Lost)
            _journeySteps[0].Text = T("1. Return to the merchant and repack");
        var onRecipientIsland = Globals.Me?.MapId == route.RecipientMapId;
        var onSourceIsland = Globals.Me?.MapId == route.SourceMapId;
        _seal.Text = T(run.Stage switch
        {
            DeliveryStage.InTransit => onRecipientIsland ? "DELIVER CARGO" : "SAIL TO DESTINATION",
            DeliveryStage.Lost => "PREPARE CARGO AGAIN",
            _ => "SEAL THE CARGO",
        });
        _seal.IsHidden = run.Stage is DeliveryStage.Delivered or DeliveryStage.Abandoned;
        _seal.TextColorOverride = _seal.IsDisabled ? Gold : Cream;
        _seal.IsDisabled = run.Stage switch
        {
            DeliveryStage.Preparing => !_boatReady || !ExportationRules.ReadyToSeal(route, run),
            DeliveryStage.InTransit => !onRecipientIsland || !run.ArrivedByShip,
            DeliveryStage.Lost => !onSourceIsland || !_boatReady,
            _ => true,
        };
        _seal.TextColorOverride = _seal.IsDisabled ? Bronze : Cream;
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
            _removeButtons[i].IsHidden = line is null ||
                run.Stage != DeliveryStage.Preparing ||
                ExportationRules.Packed(run, line.ItemId) <= 0;
        }
        for (var i = 0; i < ShelfSize; ++i)
        {
            var line = route.Lines.ElementAtOrDefault(_goodsPage * ShelfSize + i);
            _shelfLabels[i].Text = line is null ? "" :
                ItemName(line.ItemId) + "\n" +
                ExportationRules.Packed(run, line.ItemId).ToString(CultureInfo.CurrentCulture) +
                " / " + line.Quantity.ToString(CultureInfo.CurrentCulture);
            _shelfLabels[i].TextColorOverride = line is not null &&
                ExportationRules.Packed(run, line.ItemId) >= line.Quantity ? Green : Cream;
            _shelfButtons[i].IsDisabled = line is null ||
                !ExportationUiPresentation.CanPack(route, run, _goodsPage * ShelfSize + i);
            _shelfButtons[i].TooltipText = line is null ? "" :
                F("Click to pack {0} ({1}/{2})", ItemName(line.ItemId),
                    ExportationRules.Packed(run, line.ItemId), line.Quantity);
        }
        UpdateHelpVisibility();
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
    private void ToggleHelp()
    {
        _helpOpen = !_helpOpen;
        UpdateHelpVisibility();
    }

    private void UpdateHelpVisibility()
    {
        foreach (var line in _helpLines) line.IsHidden = !_helpOpen;
        _leftCrate.IsHidden = _rightCrate.IsHidden = _helpOpen;
        for (var i = 0; i < ShelfSize; i++)
            _shelfButtons[i].IsDisabled = _helpOpen || _shelfButtons[i].IsDisabled;
        for (var i = 0; i < OrderSize; i++)
            _removeButtons[i].IsDisabled = _helpOpen;
        if (_helpOpen)
        {
            _seal.IsDisabled = true;
            _previousGoods.IsDisabled = _nextGoods.IsDisabled =
                _previousOrder.IsDisabled = _nextOrder.IsDisabled = true;
        }
    }

    private void ChangeGoodsPage(int direction) { _goodsPage = Math.Max(0, _goodsPage + direction); }
    private void ChangeOrderPage(int direction) { _orderPage = Math.Max(0, _orderPage + direction); }

    private static string ItemName(Guid id) =>
        ItemDescriptor.TryGet(id, out var item) ? ClientLanguages.ContentText(item.Name) : id.ToString("N")[..8];

    protected override void Render(SkinBase skin)
    {
        // Screen-wide dimmer is deliberately subtle. The warm mini-game frame
        // below is opaque, so night / fog / the player sprite cannot shine
        // through transparent portions of the approved artwork.
        skin.Renderer.DrawColor = new Color(WorldDimOpacity, 15, 12, 9);
        skin.Renderer.DrawFilledRect(new Rect(0, 0, Width, Height));
        FillScene(skin, WoodFrame, 98, 64, 1452, 824);
        FillScene(skin, ParchmentBacking, 121, 202, 351, 581);
        FillScene(skin, CrateBacking, 476, 202, 604, 590);
        FillScene(skin, DeliveryBacking, 1087, 201, 428, 594);
        DrawTexture(skin, Background, 0, 0, DesignWidth, DesignHeight);

        // This light wash lifts the dark pixels of the semi-transparent mockup
        // without replacing its medieval wood/parchment detail or textures.
        FillScene(skin, WarmParchmentWash, 131, 211, 329, 561);
        FillScene(skin, WarmWoodWash, 489, 212, 570, 568);
        FillScene(skin, WarmWoodWash, 1098, 213, 405, 568);

        // Explicit button surfaces remain visible over the detailed sprite
        // sheet. The GWEN buttons stay fully clickable over these rectangles.
        PaintAction(skin, 143, 808, 160, 62, new Color(75, 83, 56, 29));
        if (!_seal.IsHidden)
            PaintAction(skin, 1103, 802, 396, 68,
                _seal.IsDisabled ? new Color(80, 88, 63, 38) : new Color(205, 28, 112, 47));
        // Profession experience is the server-authoritative custom profession
        // progression, not mini-game-only points.
        skin.Renderer.DrawColor = new Color(200, 38, 49, 40);
        skin.Renderer.DrawFilledRect(SceneRect(1245, 149, 236, 11));
        if (_maximumLevel || _experienceRequired > 0)
        {
            var progress = _maximumLevel ? 236 :
                (int)Math.Clamp((decimal)_experienceInLevel * 236m /
                    Math.Max(1L, _experienceRequired), 0m, 236m);
            if (progress > 0)
            {
                skin.Renderer.DrawColor = Green;
                skin.Renderer.DrawFilledRect(SceneRect(1245, 149, progress, 11));
            }
        }
        if (_route is { } r && _run is { } s)
        {
            DrawTexture(skin, SpriteDirectory + "marchand.png", 146, 282, 96, 95);
            DrawTexture(skin, SpriteDirectory + "destinataire.png", 1128, 427, 62, 63);
            DrawTexture(skin, SpriteDirectory + "bateau.png", 1182, 476, 58, 50);
            DrawTexture(skin, SpriteDirectory + "parchemin_quete.png", 1100, 684, 52, 52);
            DrawTexture(skin, SpriteDirectory + "sceau_royal.png", 307, 737, 45, 47);
            for (var i = 0; i < OrderSize; ++i)
            {
                var line = r.Lines.ElementAtOrDefault(_orderPage * OrderSize + i);
                if (line is null) continue;
                var have = ExportationRules.Packed(s, line.ItemId);
                var tint = have >= line.Quantity
                    ? new Color(98, 35, 124, 54)
                    : new Color(55, 100, 71, 37);
                FillScene(skin, tint, 141, 418 + i * 78, 305, 73);
                DrawGood(skin, line, 147, 421 + i * 78, 74, 65);
                if (have >= line.Quantity)
                {
                    FillScene(skin, new Color(255, 37, 133, 57), 397, 443 + i * 78, 15, 15);
                    FillScene(skin, new Color(255, 218, 255, 207), 401, 448 + i * 78, 7, 4);
                }
            }
            for (var i = 0; i < ShelfSize; ++i)
            {
                var line = r.Lines.ElementAtOrDefault(_goodsPage * ShelfSize + i);
                if (line is null) continue;
                var current = ExportationRules.Packed(s, line.ItemId);
                var complete = current >= line.Quantity;
                FillScene(skin, complete
                    ? new Color(112, 42, 153, 57)
                    : new Color(70, 193, 137, 63),
                    496 + i * 111, 654, 107, 120);
                FillScene(skin, complete ? Green : Bronze, 499 + i * 111, 655, 102, 3);
                DrawGood(skin, line, 510 + i * 111, 657, 79, 65);
                var itemBar = (int)(97L * current / Math.Max(1, line.Quantity));
                if (itemBar > 0)
                    FillScene(skin, Green, 502 + i * 111, 725, itemBar, 4);
            }
            // Each unit visibly fills a crate slot. At most four icons are
            // drawn per crate; counters below use the complete server counts.
            // This works for both approved Exportation sprites and item icons.
            var materials = r.Lines.Where(line => line.Group == CargoGroup.Materials)
                .SelectMany(line => Enumerable.Repeat(line,
                    Math.Min(4, ExportationRules.Packed(s, line.ItemId)))).Take(4).ToArray();
            var otherGoods = r.Lines.Where(line => line.Group != CargoGroup.Materials)
                .SelectMany(line => Enumerable.Repeat(line,
                    Math.Min(4, ExportationRules.Packed(s, line.ItemId)))).Take(4).ToArray();
            for (var i = 0; i < materials.Length; ++i)
                DrawGood(skin, materials[i], 534 + i % 2 * 109, 330 + i / 2 * 83, 88, 75);
            for (var i = 0; i < otherGoods.Length; ++i)
                DrawGood(skin, otherGoods[i], 807 + i % 2 * 106, 330 + i / 2 * 83, 85, 75);
            var materialProgress = ExportationUiPresentation.Cargo(r, s, true);
            var otherProgress = ExportationUiPresentation.Cargo(r, s, false);
            DrawCrateBar(skin, 558, 561, 178, materialProgress);
            DrawCrateBar(skin, 814, 561, 186, otherProgress);
            var total = r.Lines.Sum(l => l.Quantity);
            var completed = r.Lines.Sum(l => ExportationRules.Packed(s, l.ItemId));
            skin.Renderer.DrawColor = Green;
            var bar = SceneRect(582, 615, (int)(407d * completed / Math.Max(1, total)), 14);
            if (bar.Width > 0) skin.Renderer.DrawFilledRect(bar);
            if (s.Stage == DeliveryStage.InTransit)
                DrawTexture(skin, SpriteDirectory + "caisse_scellee.png", 783, 381, 118, 116);
            // Genuine reward icon from the configured item, not a fake coin.
            if (r.RewardQuantity > 0 && ItemDescriptor.TryGet(r.RewardItemId, out var reward))
            {
                var texture = GameContentManager.Current.GetTexture(TextureType.Item, reward.Icon);
                if (texture is not null)
                    skin.Renderer.DrawTexturedRect(texture, SceneRect(1112, 570, 49, 49), Color.White);
            }
            FillScene(skin, _boatReady ? new Color(59, 24, 112, 45) : new Color(72, 138, 44, 33),
                1168, 493, 283, 39);
        }
        if (_helpOpen)
        {
            FillScene(skin, new Color(251, 25, 38, 35), 498, 292, 556, 304);
            FillScene(skin, Bronze, 498, 292, 556, 4);
            FillScene(skin, Bronze, 498, 592, 556, 4);
            FillScene(skin, Bronze, 498, 292, 4, 304);
            FillScene(skin, Bronze, 1050, 292, 4, 304);
        }
        base.Render(skin);
    }

    private void DrawCrateBar(SkinBase skin, int x, int y, int width,
        ExportationUiPresentation.Progress progress)
    {
        FillScene(skin, new Color(255, 31, 41, 31), x, y, width, 7);
        if (progress.Required > 0)
        {
            var filled = (int)(width * (long)progress.Percent / 100);
            FillScene(skin, progress.Complete ? Green : Bronze, x, y, filled, 7);
        }
    }

    private void DrawGood(SkinBase skin, CargoLine line, int x, int y, int w, int h)
    {
        // Keep the wood/straw panels from the original GFX visible.
        // Previous opaque square covers made packed crates look like UI errors.
        // Give transparent item sprites a subtle backing on the shelf only.
        if (y >= 640)
            FillScene(skin, new Color(74, 103, 76, 50), x, y, w, h);
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

    private void FillScene(SkinBase skin, Color color, int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        skin.Renderer.DrawColor = color;
        skin.Renderer.DrawFilledRect(SceneRect(x, y, w, h));
    }

    private void PaintAction(SkinBase skin, int x, int y, int w, int h, Color fill)
    {
        // The background PNG already contains ornate action plaques. A solid
        // rectangle covered them in the previous patch; retain their detailing.
        FillScene(skin, fill, x + 7, y + 7, w - 14, h - 14);
        FillScene(skin, new Color(125, 235, 191, 115), x + 7, y + 6, w - 14, 2);
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

    private Label Title(string name, string source, int x, int y, int w, int h, int font,
        bool dark = false, bool center = false, bool wrap = false)
    {
        var label = new Label(this, "Exportation" + name)
        {
            Text = T(source), Font = Skin.DefaultFont, FontSize = font,
            AutoSizeToContents = false,
            WrappingBehavior = wrap ? WrappingBehavior.Wrapped : WrappingBehavior.NoWrap,
            TextColorOverride = dark ? DarkInk : Cream,
            TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV,
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

    /// <summary>
    /// GWEN's bitmap fonts only expose their packed atlas sizes. Requesting
    /// 28px from a font with a single 10px atlas silently renders at 10px:
    /// this caused the tiny, displaced labels in the approved 1672×941 art.
    /// Render the native glyphs at a text scale rather than changing the art.
    /// </summary>
    internal static (int NativeSize, float TextScale) ResolvePixelFont(
        IFont font, int desiredSize)
    {
        var wanted = Math.Max(8, desiredSize);
        var native = font.SupportedSizes.Count > 0 ? font.PickBestMatchFor(wanted) : wanted;
        return (native, wanted / (float)Math.Max(1, native));
    }

    /// <summary>
    /// Resolve actual rendered glyph height instead of trusting the requested
    /// bitmap atlas size. MonoRenderer may override the font size through the
    /// player's GlobalFontFamily / GlobalFontSize setting; that caused the
    /// 40px Exportation heading to render at a tiny globally forced size.
    /// </summary>
    internal static float ScaleForMeasuredFont(int desiredHeight, int actualHeight) =>
        Math.Clamp(Math.Max(8, desiredHeight) / (float)Math.Max(1, actualHeight), 0.35f, 5f);

    private void FitLabel(Label label, int designFont, int availableWidth)
    {
        var wanted = Math.Max(8, (int)Math.Round(designFont * _scale));
        var font = label.Font ?? Skin.DefaultFont;
        if (font is null) { label.FontSize = wanted; return; }

        var (nativeSize, fallbackScale) = ResolvePixelFont(font, wanted);
        // MeasureText goes through the same size override, bitmap scale and
        // UI scale as the real text renderer. Avoid a hardcoded 10px assumption.
        var naturalHeight = Skin.Renderer.MeasureText(font, nativeSize, "Hg", 1f).Y;
        var textScale = naturalHeight > 0
            ? ScaleForMeasuredFont(wanted, naturalHeight)
            : fallbackScale;

        if (!string.IsNullOrWhiteSpace(label.Text))
        {
            // Fit the full label horizontally without letting it overlap a
            // neighboring panel. The explicit multiline slots keep wrapping.
            var measured = Skin.Renderer.MeasureText(font, nativeSize, label.Text, textScale);
            var maxWidth = Math.Max(12, availableWidth - 12);
            if (measured.X > maxWidth && label.WrappingBehavior == WrappingBehavior.NoWrap)
                textScale *= maxWidth / (float)measured.X;
        }

        label.FontSize = nativeSize;
        if (!_textScaleCache.TryGetValue(label, out var last) ||
            Math.Abs(last - textScale) > .005f)
        {
            label.SetTextScale(textScale);
            _textScaleCache[label] = textScale;
        }
    }

    private void UpdateLayout()
    {
        SetPosition(0, 0); SetSize(_canvas.Width, _canvas.Height);
        _scale = Math.Min(Math.Max(1, _canvas.Width) / (double)DesignWidth,
            Math.Max(1, _canvas.Height) / (double)DesignHeight);
        _left = (int)((_canvas.Width - DesignWidth * _scale) / 2);
        _top = (int)((_canvas.Height - DesignHeight * _scale) / 2);

        if (_language != ClientLanguages.Current)
        {
            _language = ClientLanguages.Current;
            foreach (var (control, source) in _translated)
            {
                if (control is Label label) label.Text = T(source);
            }
        }

        foreach (var (control, x, y, w, h, font) in _placements)
        {
            var rect = SceneRect(x, y, w, h);
            control.SetBounds(rect.X, rect.Y, rect.Width, rect.Height);
            if (control is Label label) FitLabel(label, font, rect.Width);
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
