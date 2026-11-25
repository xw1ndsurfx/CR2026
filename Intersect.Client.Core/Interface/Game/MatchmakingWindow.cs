using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Framework.Core.Matchmaking;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.PartyLobbies;

namespace Intersect.Client.Interface.Game;

internal sealed class MatchmakingWindow : Window
{
    private readonly ImagePanel _art, _activityArt;
    private readonly ComboBox _activities;
    private readonly Label _description, _status, _clock, _message;
    private readonly ScrollControl _members;
    private readonly Button _join, _cancel, _ready, _decline;
    private MatchmakingView _state = new();
    private bool _applying;
    private long _nextPoll;
    private long _receivedAt, _readyDeadline;
    private static readonly Color Cream = new(255, 246, 235, 211), Gold = new(255, 238, 202, 132);
    internal static string T(string text) => QualityOfLifeText.Get(text, ClientLanguages.Current);
    internal Action<MatchmakingRequestPacket> SendRequest { get; set; } = packet => Intersect.Client.Networking.Network.SendPacket(packet);

    internal MatchmakingWindow(Canvas canvas) : base(canvas, T("Find an adventure"), false, nameof(MatchmakingWindow))
    {
        SetSize(Math.Min(840, canvas.Width - 20), Math.Min(650, canvas.Height - 30));
        Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        _art = new ImagePanel(this, "MatchmakingArtwork") { MouseInputEnabled = false };
        _art.SetBounds(0, 0, Width - 8, Height - 30);
        var w = Width - 56;
        var header = (int)Math.Ceiling((Height - 30) * .18);
        var descriptionHeight = Height < 540 ? 50 : 62;
        _activities = new ComboBox(this, "MatchmakingActivity") { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11 };
        _activities.SetBounds(24, header, w, 28);
        _activities.ItemSelected += (_, _) => { if (!_applying) RefreshChoice(); };
        _activityArt = new ImagePanel(this, "ActivityArtwork") { MouseInputEnabled = false, MaintainAspectRatio = true };
        _activityArt.SetBounds(24, header + 34, descriptionHeight, descriptionHeight);
        _description = Text("Description", 96, header + 34, w - 72, descriptionHeight);
        _description.WrappingBehavior = WrappingBehavior.Wrapped;
        var statusY = header + 37 + descriptionHeight;
        _status = Text("SearchStatus", 24, statusY, w, 24, Gold);
        _clock = Text("Countdown", 24, statusY + 26, w, 24, Gold);
        _members = new ScrollControl(this, "MatchmakingMembers") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true };
        _members.SetBounds(24, statusY + 57, w, Math.Max(48, Height - 147 - statusY - 57));
        _message = Text("MatchmakingMessage", 24, Height - 140, w, 57);
        _message.WrappingBehavior = WrappingBehavior.Wrapped;
        var half = (w - 10) / 2;
        _join = Button("Find a group", 24, Height - 76, half, () => Request(MatchmakingAction.Enqueue));
        _cancel = Button("Leave search", 34 + half, Height - 76, half, () => Request(MatchmakingAction.Cancel));
        _ready = Button("I am ready", 24, Height - 76, half, () => { _ready.IsDisabled = true; Request(MatchmakingAction.Ready); });
        _decline = Button("Decline", 34 + half, Height - 76, half, () => Request(MatchmakingAction.Decline));
        Apply(new(), false); Hide();
    }

    protected override void EnsureInitialized() { }
    internal void Open() { Show(); BringToFront(); Request(MatchmakingAction.Open); }
    private void Request(MatchmakingAction action)
    {
        SendRequest(new() { Action = action, EntryId = _activities.SelectedItem?.UserData as Guid? ?? Guid.Empty, ConfirmationId = _state.ConfirmationId });
        _nextPoll = Environment.TickCount64 + 4000;
    }
    private Label Text(string name, int x, int y, int width, int height, Color? color = null)
    {
        var text = new Label(this, name) { Font = _activities.Font, FontSize = 11, TextColorOverride = color ?? Cream, AutoSizeToContents = false, MouseInputEnabled = false };
        text.SetBounds(x, y, width, height); return text;
    }
    private Button Button(string name, int x, int y, int width, Action action)
    {
        var button = new Button(this, name.Replace(" ", "")) { Text = T(name), Font = _activities.Font, FontSize = 11 };
        button.SetBounds(x, y, width, 32); button.Clicked += (_, _) => action(); return button;
    }
    private void Play(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || Globals.ContentManager == null) return;
        try { var cue = InterfaceAudio.PlayConfigured(file, false); if (cue is { Loaded: true }) cue.Volume = 55; }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException) { }
    }
    internal void Apply(MatchmakingView state, bool open)
    {
        var previous = _state; _state = state; _applying = true;
        _receivedAt = Environment.TickCount64;
        _readyDeadline = _receivedAt + Math.Clamp(state.ConfirmationSecondsRemaining, 0, 180) * 1000L;
        _art.Texture = GameContentManager.Current.GetTexture(TextureType.Gui, state.Presentation.BackgroundImage);
        var selection = state.EntryId != Guid.Empty ? state.EntryId : _activities.SelectedItem?.UserData as Guid? ?? Guid.Empty;
        _activities.ClearItems();
        foreach (var choice in state.Choices)
            _activities.AddItem(T(choice.Activity == MatchmakingActivity.Dungeon ? "Dungeon" : "Naval expedition") + " · " + ClientLanguages.ContentText(choice.Name), userData: choice.Id);
        _activities.SelectByUserData(selection);
        if (_activities.SelectedItem == null && state.Choices.Length > 0) _activities.SelectByUserData(state.Choices[0].Id);
        _activities.IsDisabled = state.Status is MatchmakingStatus.Queued or MatchmakingStatus.Confirming or MatchmakingStatus.Launching;
        _members.DeleteAll();
        var y = 0;
        foreach (var member in state.Members)
        {
            var icon = new ImagePanel(_members, "ReadyIcon" + member.Id) { MouseInputEnabled = false };
            icon.Texture = GameContentManager.Current.GetTexture(TextureType.Gui, state.Presentation.IconsImage);
            if (icon.Texture is {} atlas)
            {
                var index = member.Ready ? 1 : state.Status == MatchmakingStatus.Confirming ? 5 : member.Captain ? 4 : 0;
                icon.SetTextureRect(index % 3 * atlas.Width / 3, index / 3 * atlas.Height / 2, atlas.Width / 3, atlas.Height / 2);
            }
            var rowHeight = Height < 540 ? 36 : 44;
            icon.SetBounds(0, y, rowHeight - 4, rowHeight - 4);
            var label = new Label(_members, "Member" + member.Id) { Font = _activities.Font, FontSize = 11, TextColorOverride = member.Ready ? new Color(255, 159, 235, 163) : Cream, AutoSizeToContents = false };
            label.Text = member.Name + (member.Captain ? " · " + T("Captain") : "") + (member.ShipName.Length > 0 ? " · " + member.ShipName : "") +
                " — " + T(member.Ready ? "Ready" : state.Status == MatchmakingStatus.Confirming ? "Waiting for confirmation" : "Searching");
            label.SetBounds(46, y + 5, _members.Width - 72, rowHeight - 8); label.SetToolTipText(label.Text); y += rowHeight;
        }
        _members.SetInnerSize(_members.Width - 20, Math.Max(y, _members.Height - 4));
        _message.Text = T(state.Message);
        _message.TextColorOverride = state.Error ? new Color(255, 255, 164, 140) : Cream;
        _status.Text = T(state.Status switch {
            MatchmakingStatus.Queued => "Searching for compatible players…", MatchmakingStatus.Confirming => "Group found! Everyone must confirm before departure.",
            MatchmakingStatus.Launching => "Preparing departure…", MatchmakingStatus.Departed => "Your adventure has started!", _ => "Choose an activity. Solo players and existing groups can join." });
        var confirming = state.Status == MatchmakingStatus.Confirming;
        _join.IsHidden = _cancel.IsHidden = confirming;
        _ready.IsHidden = _decline.IsHidden = !confirming;
        _ready.IsDisabled = state.Members.Any(m => m.Id == Globals.Me?.Id && m.Ready);
        _cancel.IsDisabled = state.Status != MatchmakingStatus.Queued;
        _applying = false; RefreshChoice(); UpdateClock();
        if (state.Error) Play(state.Presentation.ErrorSound);
        else if (state.ConfirmationId != Guid.Empty && state.ConfirmationId != previous.ConfirmationId) Play(state.Presentation.FoundSound);
        else if (state.Members.Any(m => m.Id == Globals.Me?.Id && m.Ready) && !previous.Members.Any(m => m.Id == Globals.Me?.Id && m.Ready)) Play(state.Presentation.ReadySound);
        else if (state.Status != previous.Status) Play(state.Status switch {
            MatchmakingStatus.Queued => state.Presentation.QueuedSound, MatchmakingStatus.Departed => state.Presentation.DepartSound,
            MatchmakingStatus.Idle or MatchmakingStatus.Cancelled => state.Presentation.CancelledSound, _ => "" });
        else if (open) Play(state.Presentation.OpenSound);
        if (open || confirming && state.ConfirmationId != previous.ConfirmationId) { Show(); BringToFront(); }
    }
    private void RefreshChoice()
    {
        var choice = _state.Choices.FirstOrDefault(c => c.Id == (_activities.SelectedItem?.UserData as Guid? ?? Guid.Empty));
        _join.IsDisabled = _state.Status is MatchmakingStatus.Queued or MatchmakingStatus.Confirming or MatchmakingStatus.Launching || choice?.CanQueue != true;
        _activityArt.Texture = choice == null ? null : GameContentManager.Current.GetTexture(TextureType.Gui, choice.Image);
        if (choice != null && _activityArt.Texture == null)
        {
            _activityArt.Texture = GameContentManager.Current.GetTexture(TextureType.Gui, _state.Presentation.IconsImage);
            if (_activityArt.Texture is {} atlas) _activityArt.SetTextureRect((choice.Activity == MatchmakingActivity.Dungeon ? 0 : 1) * atlas.Width / 3, atlas.Height / 2, atlas.Width / 3, atlas.Height / 2);
        }
        else if (_activityArt.Texture is {} portrait) _activityArt.SetTextureRect(0, 0, portrait.Width, portrait.Height);
        if (choice == null) { _description.Text = T("No activities are configured or available right now."); return; }
        var level = string.Format(T(choice.Activity == MatchmakingActivity.Expedition ? "Sailing level {0}+" : "Level {0}+"), choice.MinimumLevel);
        if (choice.MaximumLevel > 0) level = string.Format(T("Level {0}–{1}"), choice.MinimumLevel, choice.MaximumLevel);
        _description.Text = string.Format(T("Target: {0} players · Waiting: {1} · {2}"), choice.TargetPlayers, choice.WaitingPlayers, level) +
            (choice.MinimumShips > 0 ? " · " + string.Format(T("{0}–{1} ships"), choice.MinimumShips, choice.MaximumShips) : "") + "\n" + ClientLanguages.ContentText(choice.Description);
        _description.SetToolTipText(_description.Text);
        if (_state.Message.Length == 0) _message.Text = _state.Status == MatchmakingStatus.Confirming
            ? T("Confirm only when you are ready to travel with this group. Decline to leave the search.")
            : _state.Status == MatchmakingStatus.Queued ? T("Closing this window keeps you in the search. Use Leave search to cancel.")
            : !choice.CanQueue ? T(choice.Reason) : T("Your party leader enrolls the whole group. Every player confirms departure individually.");
    }
    private void UpdateClock()
    {
        _clock.Text = _state.Status == MatchmakingStatus.Confirming ? string.Format(T("Confirm within {0}s · {1}/{2} ready"),
            Math.Max(0, (int)Math.Ceiling((_readyDeadline - Environment.TickCount64) / 1000d)), _state.Members.Count(m => m.Ready), _state.Members.Length) :
            _state.Status == MatchmakingStatus.Queued ? string.Format(T("Searching: {0}s"), _state.WaitingSeconds + Math.Max(0, (Environment.TickCount64 - _receivedAt) / 1000)) : "";
        if (_state.PortName.Length > 0) _clock.Text += " · " + ClientLanguages.ContentText(_state.PortName);
    }
    public override void Think()
    {
        base.Think(); UpdateClock();
        if (!IsHidden && Environment.TickCount64 >= _nextPoll) Request(MatchmakingAction.Poll);
    }
}
