using System.Globalization;
using System.Text.Json;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.ControlInternal;
using Intersect.Client.Framework.Gwen.Input;
using Intersect.Client.Framework.Input;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Rect = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>A native, server-driven fishing scene. World characters remain in the world renderer.</summary>
internal sealed class FishingWindow : Base
{
    private const int DesignWidth = 1670, DesignHeight = 941;
    private const string FishingSheet = "fishing/items/Item_Sheet_Fishing.png";
    private const string FaunaSheet = "fishing/items/Item_Sheet_Flora&Fauna_1.png";
    private const string Components = "fishing/gui/fishing_components.png";
    private const string Effects = "fishing/fx/fishing_effects.png";
    private sealed record Placement(Base Control, int X, int Y, int W, int H, int Font);
    private readonly Canvas _canvas;
    private readonly List<Placement> _placements = [];
    private readonly Dictionary<string, IGameTexture?> _textures = [];
    private readonly FishingAudio _audio = new();
    private readonly FishingViewport _viewport;
    private readonly Label _level, _xp, _zoneName, _status, _instruction, _line, _tension, _autoStatus, _session;
    private readonly Label[] _equipmentNames = new Label[3];
    private readonly Label[] _counts = new Label[16];
    private readonly Button[] _skills = new Button[4];
    private readonly Button[] _challenge = new Button[4];
    private readonly Button _cast, _accept, _activeTab, _autoTab, _automatic, _previousPage, _nextPage;
    private readonly Button _takeLoot, _discardLoot, _lootDestination, _lootPrevious, _lootNext;
    private readonly Label[] _lootRows = new Label[3];
    private int _lootPage;
    private FishingConfiguration _configuration = new();
    private FishingState? _state;
    private FishingZoneDefinition? _zone;
    private FishingToolDefinition? _tool;
    private FishingCatchDefinition? _catch;
    private long _sequence, _receivedAt, _phaseStarted;
    private double _scale = 1;
    private int _left, _top, _inventoryPage;
    private bool _destroyed, _leftSession, _holding;
    private bool? _queuedAutomatic;
    private string _language = "";
    private string _notice = "";
    private long _noticeUntil;
    private readonly List<(Label Label, string Source)> _translated = [];
    private static readonly string[] SkillNames = ["Stabilize", "Pull", "Repair", "Dive"];
    private static readonly string[] Directions = ["Left", "Up", "Right", "Down"];
    private static readonly Color Cream = new(255, 255, 238, 194);
    private static readonly Color Gold = new(255, 241, 193, 91);

    internal Guid SessionId => _state?.SessionId ?? Guid.Empty;
    internal string EndMessage => _state?.Phase == FishingPhase.Closed ? _state.Message : "";
    internal bool ExitRequested { get; private set; }
    private static string T(string text) => FishingUiText.Get(text, ClientLanguages.Current);
    private static string F(string text, params object[] values) => string.Format(CultureInfo.CurrentCulture, T(text), values);

    internal FishingWindow(Canvas canvas) : base(canvas, nameof(FishingWindow))
    {
        _canvas = canvas;
        MouseInputEnabled = KeyboardInputEnabled = true; ShouldDrawBackground = false;
        Interface.InputBlockingComponents.Add(this);
        _viewport = new(this); Place(_viewport, 20, 121, 1182, 626);
        AddLabel("Brand", "CORPS ROYAUX", 155, 6, 500, 48, 29);
        AddLabel("Title", "PROFESSION · FISHING", 690, 5, 470, 49, 29, true);
        AddButton("Close", "×", 1608, 5, 49, 46, 27, () => ExitRequested = true);
        _level = AddLabel("Level", "", 205, 67, 150, 30, 22, true);
        _xp = AddLabel("Experience", "", 375, 67, 205, 31, 20, true);
        _zoneName = AddLabel("Zone", "", 987, 124, 202, 41, 21, true);
        _zoneName.TextColorOverride = new Color(255, 50, 30, 15);

        _activeTab = AddButton("Active", "ACTIVE FISHING", 1244, 95, 214, 49, 21, () => SetAutomatic(false));
        _autoTab = AddButton("Auto", "AUTO", 1482, 95, 158, 49, 22, () => SetAutomatic(true));
        AddLabel("Equipment", "EQUIPMENT", 1266, 158, 345, 37, 22, true);
        for (var i = 0; i < 3; i++) _equipmentNames[i] = AddLabel("Equipment" + i, "", 1245 + 137 * i, 283, 126, 35, 16, true);
        AddLabel("Inventory", "INVENTORY", 1273, 330, 333, 35, 24, true);
        AddLabel("Stacks", "Stackable catches", 1255, 366, 373, 29, 17, true);
        for (var i = 0; i < 16; i++)
            _counts[i] = AddLabel("Count" + i, "", 1259 + i % 4 * 100, 444 + i / 4 * 81, 70, 27, 18, false);
        _previousPage = AddButton("PreviousPage", "‹", 1254, 708, 42, 26, 20, () => ChangePage(-1));
        _nextPage = AddButton("NextPage", "›", 1600, 708, 42, 26, 20, () => ChangePage(1));

        AddLabel("AutomaticTitle", "AUTOMATIC FISHING", 1252, 741, 393, 39, 23, true);
        _autoStatus = AddLabel("AutomaticStatus", "", 1254, 786, 387, 39, 16, true);
        _automatic = AddButton("ToggleAutomatic", "ACTIVATE", 1274, 826, 343, 55, 19,
            () => SetAutomatic(!(_queuedAutomatic ?? (_state?.Automatic == true || _state?.AutomaticResumePending == true))));
        AddLabel("AutoLimits", "Stops without bait or inventory space", 1251, 884, 396, 37, 16, true);

        _line = AddLabel("LineCondition", "", 126, 786, 349, 33, 21);
        _tension = AddLabel("Tension", "", 129, 854, 345, 28, 15);
        for (var i = 0; i < 4; i++)
        {
            var skill = i;
            _skills[i] = AddButton("Skill" + i, "", 510 + i * 174, 775, 154, 96, 20, () => UseSkill(skill));
            _skills[i].TextAlign = Pos.Center | Pos.Bottom;
            AddLabel("SkillKey" + i, (i + 1).ToString(CultureInfo.InvariantCulture), 520 + i * 174, 780, 25, 29, 18, true);
        }
        _instruction = AddLabel("Instructions", "", 282, 888, 880, 35, 18, true);
        _status = AddLabel("Status", "", 304, 232, 660, 77, 26, true);
        _status.WrappingBehavior = WrappingBehavior.Wrapped;
        _cast = AddButton("Cast", "CAST THE LINE", 416, 360, 391, 60, 24, () => Send(FishingAction.Cast));
        _accept = AddButton("AcceptLegendary", "ACCEPT THE CHALLENGE", 416, 377, 391, 60, 23, () => Send(FishingAction.AcceptLegendary));
        for (var i = 0; i < 4; i++)
        {
            var input = i;
            _challenge[i] = AddButton("Challenge" + i, Directions[i], 322 + i * 150, 470, 140, 70, 20,
                () => Send(FishingAction.ChallengeInput, input: input));
        }
        for (var i = 0; i < _lootRows.Length; i++)
        {
            _lootRows[i] = AddLabel("LootRow" + i, "", 394, 328 + i * 34, 535, 32, 19);
            _lootRows[i].Hide();
        }
        _lootPrevious = AddButton("LootPrevious", "‹", 340, 430, 65, 28, 21, () => { _lootPage = Math.Max(0, _lootPage - 1); Update(); });
        _lootNext = AddButton("LootNext", "›", 865, 430, 65, 28, 21, () => { _lootPage++; Update(); });
        _lootPrevious.Hide(); _lootNext.Hide();
        _takeLoot = AddButton("TakeLoot", "TAKE ALL", 340, 464, 285, 60, 22, () => Send(FishingAction.TakeLoot));
        _discardLoot = AddButton("DiscardLoot", "DISCARD ALL", 645, 464, 285, 60, 22, () => Send(FishingAction.DiscardLoot));
        _discardLoot.SetToolTipText(T("Discard the fish and all loot"));
        _lootDestination = AddButton("LootDestination", "", 340, 540, 590, 48, 19,
            () => Send(FishingAction.SetLootDestination, input: _state?.BankLoot == true ? 0 : 1));
        _takeLoot.Hide(); _discardLoot.Hide(); _lootDestination.Hide();
        _session = AddLabel("Session", "", 760, 701, 420, 31, 15, false);
        _cast.IsHidden = _accept.IsHidden = true;
        foreach (var button in _challenge) button.Hide();
        UpdateLayout(); BringToFront(); Focus();
    }

    internal void Apply(FishingStatePacket packet, Guid dismissed)
    {
        try
        {
            if (packet.ConfigurationJson.Length > 0) _configuration = FishingConfiguration.FromJson(packet.ConfigurationJson);
            if (packet.StateJson.Length > 0)
            {
                var state = FishingState.FromJson(packet.StateJson);
                if (state == null || state.SessionId == dismissed || (_state?.SessionId == state.SessionId && state.Revision <= _state.Revision)) return;
                if (_state?.SessionId != state.SessionId) { _sequence = 0; _inventoryPage = 0; _leftSession = false; _queuedAutomatic = null; Release(); }
                var enteringLegendaryPrompt = state.Phase == FishingPhase.LegendaryPrompt && _state?.Phase != FishingPhase.LegendaryPrompt;
                if (_state?.SessionId != state.SessionId || _state.Phase != state.Phase) { _phaseStarted = Environment.TickCount64; _lootPage = 0; }
                _state = state; _receivedAt = Environment.TickCount64;
                _zone = _configuration.Zones.FirstOrDefault(z => z.Id == state.ZoneId);
                _tool = _configuration.Tools.FirstOrDefault(t => t.ItemId == state.ToolItemId);
                _catch = _zone?.Catches.FirstOrDefault(c => c.Id == state.CatchDefinitionId);
                _audio.Configure(_configuration, state); _audio.Apply(state);
                if (state.Phase != FishingPhase.Fighting || state.Automatic) Release();
                if (enteringLegendaryPrompt)
                {
                    var stopWasQueued = _queuedAutomatic == false;
                    _queuedAutomatic = null;
                    if (stopWasQueued && state.AutomaticResumePending) Send(FishingAction.SetMode, automatic: false);
                }
                if (_queuedAutomatic.HasValue && state.Phase is FishingPhase.Ready or FishingPhase.Caught or FishingPhase.Failed)
                {
                    var requested = _queuedAutomatic.Value; _queuedAutomatic = null;
                    if (requested != state.Automatic && state.AllowAutomatic) Send(FishingAction.SetMode, automatic: requested);
                }
                if (state.Phase == FishingPhase.Closed) { _audio.StopLoops(); ExitRequested = true; }
            }
            if (packet.Message.Length > 0)
            {
                _notice = FishingText.Get(packet.Message, ClientLanguages.Current);
                _noticeUntil = Environment.TickCount64 + 5000;
                if (packet.NoticeSoundJson.Length == 0) _audio.Play("ui_error");
            }
            if (packet.Open) { Show(); BringToFront(); Focus(); }
            Update();
        }
        catch (JsonException) { _status.Text = T("Fishing data could not be read. Reopen the activity."); Release(); }
    }

    internal void Update()
    {
        UpdateLayout(); _audio.Update();
        if (_state is not { } state) return;
        var ready = state.Phase is FishingPhase.Ready or FishingPhase.Caught or FishingPhase.Failed;
        var challenge = state.Phase == FishingPhase.LegendaryChallenge;
        var legendaryEncounter = state.Legendary && state.Phase is (FishingPhase.LegendaryPrompt or FishingPhase.Fighting or FishingPhase.LegendaryChallenge);
        _level.Text = F("Level {0}", state.Level);
        _xp.Text = state.Level >= state.MaximumLevel ? T("MAX") : F("{0:N0} / {1:N0} XP", state.ExperienceIntoLevel, state.ExperienceRequired);
        _zoneName.Text = _zone?.LocalizedNames.GetValueOrDefault(ClientLanguages.Current) is { Length: > 0 } localized ? localized : state.ZoneName;
        _equipmentNames[0].Text = T(state.ToolType.ToString());
        _equipmentNames[1].Text = T("Line");
        _equipmentNames[2].Text = state.BaitItemId == Guid.Empty ? T("No bait") : F("Bait ×{0}", state.RemainingBait);
        _line.Text = F("Line condition · {0:0}%", state.LineCondition);
        _tension.Text = F("Tension {0:0}% · Depth {1:0}%", state.Tension, state.Depth);
        var automaticSelected = state.Automatic || state.AutomaticResumePending;
        _activeTab.TextColorOverride = !automaticSelected ? new Color(255, 44, 26, 11) : Cream;
        _autoTab.TextColorOverride = automaticSelected ? Gold : Cream;
        _autoTab.IsDisabled = !state.AllowAutomatic || legendaryEncounter;
        _automatic.IsDisabled = !state.AllowAutomatic || legendaryEncounter && !state.AutomaticResumePending;
        _automatic.Text = T(_queuedAutomatic == true ? "CANCEL AUTOMATIC FISHING" : _queuedAutomatic == false ? "KEEP AUTOMATIC FISHING" :
            automaticSelected ? "STOP AUTOMATIC FISHING" : !ready ? "ACTIVATE AFTER THIS CATCH" : "ACTIVATE");
        _autoStatus.Text = T(!state.AllowAutomatic ? "Automatic fishing is disabled in this zone." :
            state.AutomaticResumePending ? "AUTO will resume when this invitation expires" :
            legendaryEncounter ? "Legendary catches require manual fishing" :
            _queuedAutomatic == true ? "Automatic starts after this catch" : _queuedAutomatic == false ? "Automatic stops after this catch" : state.Automatic ?
            state.Phase == FishingPhase.LegendaryPrompt ? "Legendary catch: waiting for you" : "Fishing continues while connected" : "Chain catches while connected");
        _instruction.Text = T(challenge ? "Follow the highlighted direction before time runs out" :
            state.Phase == FishingPhase.Fighting ? "Hold: reel in  ·  Release: let the line run" : "Choose active fishing or automatic fishing");
        var loot = state.Phase == FishingPhase.Loot;
        _takeLoot.IsHidden = _discardLoot.IsHidden = !loot;
        var lootEntries = LootEntries(state);
        _lootPage = Math.Clamp(_lootPage, 0, Math.Max(0, (lootEntries.Count - 1) / 3));
        _lootPrevious.IsHidden = _lootNext.IsHidden = !loot || lootEntries.Count <= 3;
        _lootPrevious.IsDisabled = _lootPage == 0;
        _lootNext.IsDisabled = (_lootPage + 1) * 3 >= lootEntries.Count;
        for (var i = 0; i < _lootRows.Length; i++)
        {
            var entry = lootEntries.ElementAtOrDefault(_lootPage * 3 + i);
            _lootRows[i].IsHidden = !loot || entry == null;
            _lootRows[i].Text = entry == null ? "" : F("{0} ×{1}", ItemName(entry.ItemId), entry.Quantity);
            _lootRows[i].SetToolTipText(entry == null ? "" : ItemName(entry.ItemId));
        }
        _lootDestination.IsHidden = !state.CanBankLoot || !(loot || ready);
        _lootDestination.Text = T(state.BankLoot ? "LOOT TO BANK · PREMIUM" : "LOOT TO INVENTORY");
        _cast.IsHidden = !ready || state.Automatic;
        _accept.IsHidden = state.Phase != FishingPhase.LegendaryPrompt;
        _cast.Text = T(state.Phase == FishingPhase.Caught ? "CAST AGAIN" : "CAST THE LINE");
        var remaining = Math.Max(0, state.RemainingMs - (Environment.TickCount64 - _receivedAt));
        _cast.IsDisabled = ready && remaining > 0;
        _status.Text = state.Phase switch
        {
            FishingPhase.Ready => T("Ready to fish"),
            FishingPhase.Waiting => T("Waiting for a bite…"),
            FishingPhase.Fighting => T(state.Resisting ? "The fish resists!\nRelease the reel" : "Reel in!\nHold to bring the catch closer"),
            FishingPhase.LegendaryPrompt => F("A legendary creature!\nAccept within {0}s", (remaining + 999) / 1000),
            FishingPhase.LegendaryChallenge => F("Legendary challenge · {0}/{1}\n{2} · {3:0.0}s", state.ChallengeStep + 1, state.ChallengeSteps,
                T(Directions[Math.Clamp(state.ChallengeExpectedInput, 0, 3)]), remaining / 1000d),
            FishingPhase.Retrieving => T("Bringing the catch to the surface…"),
            FishingPhase.Loot => F("{0} ×{1}\nTake all loot or discard it", ItemName(state.CatchItemId), state.CatchQuantity),
            FishingPhase.Caught => F("{0} ×{1}\n+{2} XP", ItemName(state.CatchItemId), state.CatchQuantity, state.ExperienceGained),
            FishingPhase.Failed => state.Message.Length > 0 ? FishingText.Get(state.Message, ClientLanguages.Current) : T("The catch escaped"),
            _ => "",
        };
        _status.TextColorOverride = state.Legendary ? Gold : state.Resisting ? new Color(255, 255, 205, 160) : Cream;
        if (state.Phase is (FishingPhase.Ready or FishingPhase.Loot) && !string.IsNullOrEmpty(state.Message)) _status.Text = state.Message;
        if (Environment.TickCount64 < _noticeUntil) _status.Text = _notice;
        for (var i = 0; i < 4; i++)
        {
            _challenge[i].IsHidden = !challenge;
            _challenge[i].TextColorOverride = i == state.ChallengeExpectedInput ? Gold : Cream;
            var cooldown = Math.Max(0, state.SkillCooldownsMs.ElementAtOrDefault(i) - (Environment.TickCount64 - _receivedAt));
            _skills[i].IsDisabled = !state.SkillAvailable.ElementAtOrDefault(i);
            _skills[i].TextColorOverride = _skills[i].IsDisabled ? new Color(255, 150, 137, 117) : Cream;
            _skills[i].Text = T(SkillNames[i]) + (cooldown > 0 ? F(" ({0}s)", (cooldown + 999) / 1000) : "");
        }
        _inventoryPage = Math.Min(_inventoryPage, Math.Max(0, (state.Inventory.Count - 1) / 16));
        for (var i = 0; i < _counts.Length; i++)
        {
            var entry = state.Inventory.ElementAtOrDefault(i + _inventoryPage * 16);
            _counts[i].Text = entry == null ? "" : F("×{0:N0}", entry.Quantity);
            _counts[i].SetToolTipText(entry == null ? "" : ItemName(entry.ItemId));
        }
        _previousPage.IsHidden = _nextPage.IsHidden = state.Inventory.Count <= 16;
        _previousPage.IsDisabled = _inventoryPage == 0;
        _nextPage.IsDisabled = (_inventoryPage + 1) * 16 >= state.Inventory.Count;
        _session.Text = F("This session: {0} catches", state.CatchesThisSession);
    }

    private void ChangePage(int direction) { _inventoryPage = Math.Max(0, _inventoryPage + direction); _audio.Play("ui_select"); }
    private void SetAutomatic(bool automatic)
    {
        if (_state is not { AllowAutomatic: true }) return;
        Release();
        if (automatic && _state.Legendary && _state.Phase is (FishingPhase.LegendaryPrompt or FishingPhase.Fighting or FishingPhase.LegendaryChallenge))
        { _queuedAutomatic = null; return; }
        if ((_state.Automatic || _state.AutomaticResumePending) == automatic) { _queuedAutomatic = null; return; }
        if (!automatic && _state.Phase == FishingPhase.LegendaryPrompt && _state.AutomaticResumePending)
        { _queuedAutomatic = null; Send(FishingAction.SetMode, automatic: false); return; }
        if (_state.Phase is not (FishingPhase.Ready or FishingPhase.Caught or FishingPhase.Failed))
        { _queuedAutomatic = automatic; _audio.Play("ui_confirm"); return; }
        _queuedAutomatic = null; Send(FishingAction.SetMode, automatic: automatic);
    }
    private void UseSkill(int skill)
    {
        if (_state?.SkillAvailable.ElementAtOrDefault(skill) != true) return;
        Send(FishingAction.UseSkill, input: skill);
    }
    private void Send(FishingAction action, bool automatic = false, bool reeling = false, int input = 0)
    {
        if (_state == null || _leftSession) return;
        _noticeUntil = 0;
        Networking.Network.SendPacket(new FishingRequestPacket { Action = action, SessionId = _state.SessionId,
            Sequence = ++_sequence, Automatic = automatic, Reeling = reeling, Input = input });
        if (action is FishingAction.Cast or FishingAction.AcceptLegendary) _audio.Play("ui_confirm");
    }
    private void Hold()
    {
        if (_holding || _state is not { Phase: FishingPhase.Fighting, Automatic: false }) return;
        _holding = true; Send(FishingAction.SetReeling, reeling: true);
    }
    private void Release()
    {
        if (_holding) { _holding = false; Send(FishingAction.SetReeling, reeling: false); }
        if (InputHandler.MouseFocus == _viewport) InputHandler.MouseFocus = null;
    }
    internal void Leave()
    {
        Release();
        // Audio is optional: a failing sound device must not leave a live server
        // session that subsequent Event interactions would reopen and dismiss.
        if (!_leftSession) { Send(FishingAction.Leave); _leftSession = true; }
        _audio.StopLoops();
    }

    protected override bool OnChar(char character)
    {
        if (character is >= '1' and <= '4') { UseSkill(character - '1'); return true; }
        return base.OnChar(character);
    }
    protected override bool OnKeyPressed(Key key, bool down = true)
    {
        if (key == Key.Escape && down) { ExitRequested = true; return true; }
        if (key == Key.Space) { if (down) Hold(); else Release(); return true; }
        if (down && _state?.Phase == FishingPhase.LegendaryChallenge)
        {
            var input = key switch { Key.Left => 0, Key.Up => 1, Key.Right => 2, Key.Down => 3, _ => -1 };
            if (input >= 0) { Send(FishingAction.ChallengeInput, input: input); return true; }
        }
        return base.OnKeyPressed(key, down);
    }
    protected override void OnLostKeyboardFocus() { Release(); base.OnLostKeyboardFocus(); }

    protected override void Render(SkinBase skin)
    {
        Fill(skin, 0, 0, DesignWidth, DesignHeight, new Color(255, 28, 15, 9));
        DrawScene(skin);
        Texture(skin, "fishing/gui/fishing_shell.png", 0, 0, DesignWidth, DesignHeight);
        if (_state is { } state)
        {
            var xp = state.Level >= state.MaximumLevel ? 1 : state.ExperienceRequired > 0 ? state.ExperienceIntoLevel / (double)state.ExperienceRequired : 0;
            FilledBar(skin, 590, 70, 575, 20, xp, 0);
            FilledBar(skin, 132, 827, 333, 22, state.LineCondition / 100, state.LineCondition < 30 ? 2 : 0);
            Bar(skin, 129, 881, 345, 4, state.Tension / 100, state.Tension >= 75 ? new Color(255, 250, 65, 36) : Gold);
            ButtonSurface(skin, _automatic, 1274, 826, 343, 55, true);
            for (var i = 0; i < 4; i++) ButtonSurface(skin, _skills[i], 510 + i * 174, 775, 154, 96, false);
            DrawItem(skin, state.ToolItemId, 1275, 207, 67, 65);
            Sprite(skin, FishingSheet, 14 * 32, 0, 32, 32, 1407, 207, 67, 65);
            if (state.BaitItemId != Guid.Empty) DrawItem(skin, state.BaitItemId, 1544, 209, 67, 63);
            for (var i = 0; i < 16; i++)
                if (state.Inventory.ElementAtOrDefault(i + _inventoryPage * 16) is { } entry)
                {
                    if (entry.ItemId == state.CatchItemId && state.Phase == FishingPhase.Caught)
                        Sprite(skin, Components, 500, 817, 153, 155, 1249 + i % 4 * 100, 397 + i / 4 * 81, 94, 77);
                    DrawItem(skin, entry.ItemId, 1258 + i % 4 * 100, 400 + i / 4 * 81, 74, 52);
                }
            foreach (var (column, index) in new[] { 6, 8, 3, 18 }.Select((column, index) => (column, index)))
                Sprite(skin, FishingSheet, column * 32, 0, 32, 32, 565 + index * 174, 782, 47, 48,
                    _skills[index].IsDisabled ? new Color(170, 190, 178, 160) : Color.White);
            if (state.Phase == FishingPhase.Loot)
            {
                Panel(skin, 310, 320, 650, 285, new Color(245, 36, 24, 18));
                var lootEntries = LootEntries(state);
                for (var i = 0; i < 3; i++)
                    if (lootEntries.ElementAtOrDefault(_lootPage * 3 + i) is { } loot)
                        DrawItem(skin, loot.ItemId, 342, 328 + i * 34, 38, 30);
                if (!_lootPrevious.IsHidden) ButtonSurface(skin, _lootPrevious, 340, 430, 65, 28, false);
                if (!_lootNext.IsHidden) ButtonSurface(skin, _lootNext, 865, 430, 65, 28, false);
                ButtonSurface(skin, _takeLoot, 340, 464, 285, 60, true);
                ButtonSurface(skin, _discardLoot, 645, 464, 285, 60, false);
                // A drawn bin stays readable independently of the system font's emoji support.
                Fill(skin, 661, 487, 13, 17, Cream);
                Fill(skin, 658, 483, 19, 3, Cream);
                Fill(skin, 664, 479, 7, 3, Cream);
            }
            if (!_lootDestination.IsHidden) ButtonSurface(skin, _lootDestination, 340, 540, 590, 48, false);
            if (!_cast.IsHidden) ButtonSurface(skin, _cast, 416, 360, 391, 60, true);
            if (!_accept.IsHidden) ButtonSurface(skin, _accept, 416, 377, 391, 60, true);
            if (state.Phase == FishingPhase.LegendaryChallenge)
                for (var i = 0; i < 4; i++) Panel(skin, 322 + i * 150, 470, 140, 70,
                    i == state.ChallengeExpectedInput ? new Color(250, 109, 78, 29) : new Color(230, 35, 29, 23));
            // Readable instruction plaque, with no baked-in labels or inventory values.
            if (state.Phase != FishingPhase.Waiting) Panel(skin, 300, 228, 668, 85, new Color(205, 36, 24, 18));
        }
        base.Render(skin);
    }

    private void DrawScene(SkinBase skin)
    {
        Fill(skin, 20, 121, 1182, 626, new Color(255, 14, 186, 216));
        Background(skin, _zone?.Assets.Background ?? "fishing/backgrounds/Water_Background4.png");
        Texture(skin, _zone?.Assets.Foreground ?? "fishing/backgrounds/Water_Background5.png", 20, 121, 1182, 626);
        Texture(skin, _zone?.Assets.Decorations ?? "fishing/gui/coral_foreground.png", 20, 121, 1182, 626);
        var now = Environment.TickCount64 / 1000d;
        var loopFrame = (int)(Environment.TickCount64 / 125 % 4);
        for (var i = 0; i < 4; i++) Effect(skin, 2, (loopFrame + i) % 4, 245 + i * 263, 500 - (int)(now * 16 + i * 27) % 130, 34, 104);
        var visibleCatches = _zone?.Catches.Where(c => !c.Legendary && (_tool == null || c.ToolItemIds.Count == 0 || c.ToolItemIds.Contains(_tool.ItemId))).Take(12).ToArray() ?? [];
        for (var i = 0; i < visibleCatches.Length; i++)
        {
            var x = 95 + (i * 147 + now * (7 + i % 4 * 3)) % 1000;
            var y = 340 + i % 4 * 72 + Math.Sin(now + i) * 8;
            DrawCatch(skin, visibleCatches[i], (int)x, (int)y, 92, 66);
        }
        // Decorative seabed fauna use the supplied small atlas; actual reward icons always use authored catches.
        Sprite(skin, FaunaSheet, 0, 64, 32, 32, 815, 674, 67, 52);
        Sprite(skin, FishingSheet, 32, 64, 32, 32, 379, 678, 55, 51);
        if (_state is not { } state || state.Phase is FishingPhase.Ready or FishingPhase.Closed) return;
        var phaseElapsed = Environment.TickCount64 - _phaseStarted;
        Effect(skin, 0, loopFrame, 431, 291, 100, 36);
        if (state.Phase == FishingPhase.Waiting && phaseElapsed < 500)
            Effect(skin, 1, Math.Clamp((int)(phaseElapsed / 125), 0, 3), 406, 257, 142, 92);
        var targetX = 685 - state.Progress * 2.1;
        var targetY = 322 + state.Depth * 3.1 + Math.Sin(now * 2.4) * (state.Resisting ? 11 : 4);
        if (state.Phase is FishingPhase.Waiting) targetY = 355 + state.Depth * 2.4;
        // Reeling raises both the hook and its attached fish toward the water surface.
        if (state.Phase is FishingPhase.Fighting or FishingPhase.LegendaryChallenge)
            targetY = 291 + (targetY - 291) * (1 - state.Progress * .7 / 100);
        if (state.Phase is FishingPhase.Retrieving)
        {
            var lift = Math.Clamp(phaseElapsed / 1200d, 0, 1);
            targetY = 291 + (322 + state.Depth * 3.1 - 291) * .3;
            targetY += (291 - targetY) * lift;
        }
        if (state.Phase is FishingPhase.Caught or FishingPhase.Loot) targetY = 291;
        var endX = (int)targetX - 23; var endY = (int)targetY + 10;
        // A quadratic line enters from outside the view; no shore, dock, or substitute avatar.
        var previousX = 397d; var previousY = 121d;
        for (var point = 1; point <= 32; point++)
        {
            var t = point / 32d;
            var x = (1 - t) * (1 - t) * 397 + 2 * (1 - t) * t * 458 + t * t * endX;
            var y = (1 - t) * (1 - t) * 121 + 2 * (1 - t) * t * (endY - 3) + t * t * endY;
            Line(skin, (int)previousX, (int)previousY, (int)x, (int)y, Cream);
            previousX = x; previousY = y;
        }
        Sprite(skin, FishingSheet, 14 * 32, 0, 32, 32, endX - 20, endY - 14, 27, 27);
        if (_catch != null && state.Phase is not FishingPhase.Waiting)
        {
            DrawCatch(skin, _catch, (int)targetX - 14, (int)targetY - 24, 105, 72);
            Bar(skin, (int)targetX - 8, (int)targetY + 52, 94, 7, state.Progress / 100, Gold);
            if (state.Resisting) Sprite(skin, Components, 929, 817, 62, 155, (int)targetX + 15, (int)targetY - 94, 24, 58);
            if (state.Legendary)
            {
                Sprite(skin, Components, 1266, 817, 156, 150, (int)targetX + 70, (int)targetY - 54, 42, 40);
                if (phaseElapsed < 500) Effect(skin, 3, (int)(phaseElapsed / 125), (int)targetX - 40, (int)targetY - 57, 150, 150);
            }
        }
        // Depth ticks stay schematic because zones are authorable in relative depth units.
        Line(skin, 34, 317, 34, 658, Cream);
        for (var i = 0; i <= 4; i++) Line(skin, 34, 317 + i * 85, 48, 317 + i * 85, Cream);
    }

    private static IReadOnlyList<FishingInventoryEntry> LootEntries(FishingState state) => state.Loot.Count > 0
        ? state.Loot : state.CatchItemId == Guid.Empty ? [] : [new() { ItemId = state.CatchItemId, Quantity = state.CatchQuantity }];
    private static string ItemName(Guid id) => ItemDescriptor.TryGet(id, out var item) ? ClientLanguages.ContentText(item.Name) : "";
    private void DrawItem(SkinBase skin, Guid itemId, int x, int y, int width, int height)
    {
        if (ItemDescriptor.TryGet(itemId, out var item))
        {
            var texture = GameContentManager.Current.GetTexture(TextureType.Item, item.Icon);
            if (texture != null) skin.Renderer.DrawTexturedRect(texture, SceneRect(x, y, width, height), Color.White);
        }
    }
    private void DrawCatch(SkinBase skin, FishingCatchDefinition definition, int x, int y, int width, int height)
    {
        // Items are the single source of appearance; animate their icon with a gentle swim.
        if (ItemDescriptor.TryGet(definition.ItemId, out var item) &&
            GameContentManager.Current.GetTexture(TextureType.Item, item.Icon) is { } texture)
        {
            var swim = Math.Sin(Environment.TickCount64 / 180d + definition.Id.GetHashCode());
            var aspect = texture.Width / (double)Math.Max(1, texture.Height);
            var w = Math.Min(width, (int)(height * aspect));
            var h = Math.Min(height, (int)(width / aspect));
            var pulse = 1 + swim * .035;
            skin.Renderer.DrawTexturedRect(texture, SceneRect(x + (width - w) / 2,
                y + (height - h) / 2 + (int)(swim * 3), Math.Max(1, (int)(w * pulse)), Math.Max(1, h)), Color.White);
        }
    }
    private IGameTexture? Texture(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (!_textures.TryGetValue(name, out var texture)) _textures[name] = texture = GameContentManager.Current.GetTexture(TextureType.Gui, name);
        return texture;
    }
    private void Texture(SkinBase skin, string name, int x, int y, int width, int height)
    {
        if (Texture(name) is { } texture) skin.Renderer.DrawTexturedRect(texture, SceneRect(x, y, width, height), Color.White);
    }
    private void Background(SkinBase skin, string name)
    {
        if (Texture(name) is not { } texture) return;
        const double aspect = 1182d / 626;
        var sourceWidth = Math.Min(texture.Width, texture.Height * aspect);
        var sourceHeight = sourceWidth / aspect;
        var sourceX = (texture.Width - sourceWidth) / 2;
        // Portrait water is a genuine depth strip: move the camera instead of flattening the asset.
        var verticalPosition = texture.Height > texture.Width ? Math.Clamp((_state?.Depth ?? 0) / 100, 0, 1) : .5;
        var sourceY = (texture.Height - sourceHeight) * verticalPosition;
        skin.Renderer.DrawTexturedRect(texture, SceneRect(20, 121, 1182, 626), Color.White,
            (float)(sourceX / texture.Width), (float)(sourceY / texture.Height),
            (float)((sourceX + sourceWidth) / texture.Width), (float)((sourceY + sourceHeight) / texture.Height));
    }
    private void ButtonSurface(SkinBase skin, Button button, int x, int y, int w, int h, bool primary)
    {
        var state = button.IsDisabled ? 3 : button.IsActive ? 2 : button.IsHovered ? 1 : 0;
        Sprite(skin, Components, new[] { 35, 419, primary ? 802 : 803, 1187 }[state], primary ? 349 : 92,
            primary && state == 2 ? 316 : 315, primary ? 104 : 105, x, y, w, h);
    }
    private void FilledBar(SkinBase skin, int x, int y, int w, int h, double fraction, int color)
    {
        Fill(skin, x, y, w, h, new Color(255, 19, 20, 11));
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction > 0) Sprite(skin, Components, new[] { 35, 419, 803 }[color], 629,
            Math.Max(1, (int)(315 * fraction)), 29, x, y, Math.Max(1, (int)(w * fraction)), h);
    }
    private void Effect(SkinBase skin, int row, int frame, int x, int y, int w, int h)
    {
        var starts = new[] { 0, 314, 627, 940, 1254 };
        frame = Math.Clamp(frame, 0, 3);
        Sprite(skin, Effects, starts[frame], starts[row], starts[frame + 1] - starts[frame], starts[row + 1] - starts[row], x, y, w, h);
    }
    private void Sprite(SkinBase skin, string name, int sx, int sy, int sw, int sh, int x, int y, int width, int height, Color? tint = null)
    {
        if (Texture(name) is not { } texture || sw <= 0 || sh <= 0) return;
        skin.Renderer.DrawTexturedRect(texture, SceneRect(x, y, width, height), tint ?? Color.White,
            (sx + .5f) / texture.Width, (sy + .5f) / texture.Height, (sx + sw - .5f) / texture.Width, (sy + sh - .5f) / texture.Height);
    }
    private Rect SceneRect(int x, int y, int w, int h) => new(_left + (int)Math.Round(x * _scale), _top + (int)Math.Round(y * _scale),
        Math.Max(1, (int)Math.Round(w * _scale)), Math.Max(1, (int)Math.Round(h * _scale)));
    private void Fill(SkinBase skin, int x, int y, int w, int h, Color color)
    { skin.Renderer.DrawColor = color; skin.Renderer.DrawFilledRect(SceneRect(x, y, w, h)); }
    private void Bar(SkinBase skin, int x, int y, int w, int h, double fraction, Color color)
    {
        Fill(skin, x, y, w, h, new Color(255, 21, 22, 10));
        if (fraction > 0) Fill(skin, x + 1, y + 1, Math.Max(1, (int)((w - 2) * Math.Clamp(fraction, 0, 1))), h - 2, color);
    }
    private void Panel(SkinBase skin, int x, int y, int w, int h, Color color)
    { Fill(skin, x - 2, y - 2, w + 4, h + 4, Gold); Fill(skin, x, y, w, h, color); }
    private void Line(SkinBase skin, int x, int y, int endX, int endY, Color color)
    {
        skin.Renderer.DrawColor = color;
        skin.Renderer.DrawLine(_left + (int)(x * _scale), _top + (int)(y * _scale), _left + (int)(endX * _scale), _top + (int)(endY * _scale));
    }
    private Label AddLabel(string name, string source, int x, int y, int w, int h, int font, bool center = false)
    {
        var label = new Label(this, "Fishing" + name) { Text = T(source), Font = Skin.DefaultFont,
            FontSize = font, TextColorOverride = Cream, AutoSizeToContents = false, MouseInputEnabled = false,
            KeyboardInputEnabled = false, TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV };
        Place(label, x, y, w, h, font);
        if (source.Length > 0) _translated.Add((label, source));
        return label;
    }
    private Button AddButton(string name, string source, int x, int y, int w, int h, int font, Action clicked)
    {
        var button = new Button(this, "Fishing" + name) { Text = T(source), Font = Skin.DefaultFont,
            FontSize = font, TextColorOverride = Cream, ShouldDrawBackground = false };
        foreach (var state in Enum.GetValues<ButtonSoundState>()) button.SetSound(state, null);
        button.Clicked += (_, _) => { clicked(); Focus(); };
        button.HoverEnter += (_, _) => _audio.Play("ui_select");
        Place(button, x, y, w, h, font);
        if (source.Length > 0) _translated.Add((button, source));
        return button;
    }
    private void Place(Base control, int x, int y, int w, int h, int font = 0)
    { control.Dock = Pos.None; _placements.Add(new(control, x, y, w, h, font)); }
    private void UpdateLayout()
    {
        SetPosition(0, 0); SetSize(_canvas.Width, _canvas.Height);
        _scale = Math.Min(Math.Max(1, _canvas.Width) / (double)DesignWidth, Math.Max(1, _canvas.Height) / (double)DesignHeight);
        _left = (int)((_canvas.Width - DesignWidth * _scale) / 2); _top = (int)((_canvas.Height - DesignHeight * _scale) / 2);
        foreach (var placement in _placements)
        {
            var rect = SceneRect(placement.X, placement.Y, placement.W, placement.H);
            placement.Control.SetBounds(rect.X, rect.Y, rect.Width, rect.Height);
            if (placement.Control is Label label && placement.Font > 0) label.FontSize = Math.Max(8, (int)Math.Round(placement.Font * _scale));
        }
        if (_language == ClientLanguages.Current) return;
        _language = ClientLanguages.Current;
        foreach (var (label, source) in _translated) label.Text = T(source);
    }
    internal void Destroy(bool playClose = true)
    {
        if (_destroyed) return;
        _destroyed = true;
        try
        {
            Release();
            if (playClose) _audio.Close(_state?.Phase == FishingPhase.Closed && !string.IsNullOrEmpty(_state.SoundCue) ? _state.SoundCue : "session_end");
            else _audio.Dispose();
        }
        finally
        {
            Interface.InputBlockingComponents.Remove(this);
            Hide(); Parent?.RemoveChild(this, false); Dispose();
        }
    }

    private sealed class FishingViewport(FishingWindow owner) : Base(owner, "FishingWater")
    {
        protected override void Render(SkinBase skin) { }
        protected override void OnMouseDown(MouseButton button, Point position, bool userAction = true)
        {
            base.OnMouseDown(button, position, userAction);
            if (button != MouseButton.Left) return;
            owner.Focus(); InputHandler.MouseFocus = this; owner.Hold();
        }
        protected override void OnMouseUp(MouseButton button, Point position, bool userAction = true)
        {
            base.OnMouseUp(button, position, userAction);
            if (button == MouseButton.Left) owner.Release();
        }
    }
}
