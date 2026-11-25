using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.MountRacing;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;

namespace Intersect.Client.Interface.Game;

internal sealed class MountRaceWindow : Window
{
    private readonly ImagePanel _art;
    private readonly ComboBox _courses;
    private readonly Label _description, _status, _message;
    private readonly ScrollControl _rows;
    private readonly Button _join, _practice, _ready, _leave, _records;
    private MountRaceView _state = new();
    private bool _applying, _showRecords;
    private long _nextPoll, _received;
    private Guid _resultShown;
    internal Func<Guid> PlayerId { get; set; } = () => Globals.Me?.Id ?? Guid.Empty;
    internal Action<MountRaceRequestPacket> SendRequest { get; set; } = p => Networking.Network.SendPacket(p);
    internal static string T(string s) => QualityOfLifeText.Get(s, ClientLanguages.Current);
    internal static string Time(long milliseconds) => TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)).ToString(@"mm\:ss\.ff");
    internal MountRaceWindow(Canvas canvas) : base(canvas, T("Royal Races"), false, nameof(MountRaceWindow))
    {
        SetSize(Math.Min(840, canvas.Width - 20), Math.Min(660, canvas.Height - 30));
        Alignment = [Alignments.Center]; DeleteOnClose = false; DisableResizing();
        _art = new ImagePanel(this) { MouseInputEnabled = false }; _art.SetBounds(0, 0, Width - 8, Height - 30);
        var top = (int)Math.Ceiling((Height - 30) * .21); var w = Width - 56;
        _courses = new ComboBox(this, "RaceCourse") { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11 };
        _courses.SetBounds(24, top, w, 28); _courses.ItemSelected += (_, _) => { if (!_applying) { RefreshChoice(); Request(MountRaceAction.Poll); } };
        _description = Label(this, "RaceDescription", 24, top + 34, w, 54); _description.WrappingBehavior = WrappingBehavior.Wrapped;
        _status = Label(this, "RaceStatus", 24, top + 92, w, 27); _status.TextColorOverride = new Color(255, 242, 207, 139);
        _rows = new ScrollControl(this, "RaceRows") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true };
        _rows.SetBounds(24, top + 122, w, Math.Max(45, Height - top - 270));
        _message = Label(this, "RaceMessage", 24, Height - 142, w, 48); _message.WrappingBehavior = WrappingBehavior.Wrapped;
        var bw = (w - 16) / 3;
        _join = Button(this, "Join race", 24, Height - 85, bw, () => Request(MountRaceAction.Join));
        _practice = Button(this, "Solo practice", 32 + bw, Height - 85, bw, () => Request(MountRaceAction.Practice));
        _ready = Button(this, "I am ready", 24, Height - 85, bw, () => { _ready.IsDisabled = true; Request(MountRaceAction.Ready); });
        _leave = Button(this, "Leave race", 32 + bw, Height - 85, bw, () => Request(MountRaceAction.Leave));
        _records = Button(this, "Records and prizes", 40 + bw * 2, Height - 85, bw, () => { _showRecords = !_showRecords; Request(MountRaceAction.Poll); Rebuild(); });
        Apply(new(), false); Hide();
    }
    protected override void EnsureInitialized() { }
    internal void Open() { Show(); BringToFront(); Request(MountRaceAction.Open); }
    internal void Request(MountRaceAction action, Guid record = default)
    { SendRequest(new() { Action = action, CourseId = _courses.SelectedItem?.UserData as Guid? ?? Guid.Empty, RaceId = _state.RaceId, RecordId = record }); _nextPoll = Environment.TickCount64 + 3000; }
    internal static Label Label(Base parent, string name, int x, int y, int width, int height)
    {
        var l = new Label(parent, name) { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont, FontSize = 11,
            AutoSizeToContents = false, TextColorOverride = new Color(255, 245, 231, 201), MouseInputEnabled = false };
        l.SetBounds(x, y, width, height); return l;
    }
    internal static Button Button(Base parent, string text, int x, int y, int width, Action action)
    {
        var b = new Button(parent, text.Replace(" ", "")) { Text = T(text), Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont, FontSize = 11 };
        b.SetBounds(x, y, width, 32); b.Clicked += (_, _) => action(); return b;
    }
    internal static void Icon(ImagePanel image, string file, int index)
    {
        image.Texture = GameContentManager.Current.GetTexture(TextureType.Gui, file);
        if (image.Texture is {} tex) image.SetTextureRect(index % 3 * tex.Width / 3, index / 3 * tex.Height / 2, tex.Width / 3, tex.Height / 2);
    }
    internal static void Play(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || Globals.ContentManager == null) return;
        try { var cue = InterfaceAudio.PlayConfigured(file, false); if (cue is { Loaded: true }) cue.Volume = 55; }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException) { }
    }
    internal void Apply(MountRaceView view, bool open)
    {
        var previous = _state; _state = view; _received = Environment.TickCount64; _applying = true;
        _art.Texture = GameContentManager.Current.GetTexture(TextureType.Gui, view.Presentation.Background);
        var selected = view.CourseId != Guid.Empty ? view.CourseId : _courses.SelectedItem?.UserData as Guid? ?? Guid.Empty;
        _courses.ClearItems(); foreach (var c in view.Courses) _courses.AddItem(ClientLanguages.ContentText(c.Name), userData: c.Id);
        _courses.SelectByUserData(selected); if (_courses.SelectedItem == null && view.Courses.Length > 0) _courses.SelectByUserData(view.Courses[0].Id);
        _courses.IsDisabled = view.Phase is MountRacePhase.Queued or MountRacePhase.Ready or MountRacePhase.Countdown or MountRacePhase.Running;
        var active = view.RaceId != Guid.Empty || view.Phase == MountRacePhase.Queued;
        var done = Done(view);
        _join.IsHidden = _practice.IsHidden = active; _ready.IsHidden = !active || done; _leave.IsHidden = !active;
        _leave.Text = T(done ? "Back" : "Leave race");
        _ready.IsDisabled = view.Phase != MountRacePhase.Ready || view.Riders.Any(r => r.Id == PlayerId() && r.Ready);
        _records.IsDisabled = view.OnTrack || view.Phase == MountRacePhase.Ready;
        _message.Text = T(view.Message); _message.TextColorOverride = view.Error ? new Color(255, 255, 168, 136) : new Color(255, 245, 231, 201);
        if (view.Phase == MountRacePhase.Ready && view.RaceId != previous.RaceId) { _showRecords = false; Play(view.Presentation.ReadySound); Show(); BringToFront(); }
        if (view.Phase == MountRacePhase.Countdown && previous.Phase != view.Phase) Hide();
        if (done && view.RaceId != Guid.Empty && _resultShown != view.RaceId)
        {
            _resultShown = view.RaceId; _showRecords = false; Show(); BringToFront();
            Play(view.Riders.Any(r => r.Id == PlayerId() && r.Place > 0) ? view.Presentation.FinishSound : view.Presentation.CancelSound);
        }
        if (view.Message == "Race prize claimed!") Play(view.Presentation.RewardSound);
        else if (open && previous.Phase == view.Phase) Play(view.Presentation.OpenSound);
        _applying = false; RefreshChoice(); Rebuild();
        if (open && !view.OnTrack) { Show(); BringToFront(); }
    }
    private void RefreshChoice()
    {
        var choice = _state.Courses.FirstOrDefault(c => c.Id == (_courses.SelectedItem?.UserData as Guid? ?? Guid.Empty));
        _join.IsDisabled = choice?.CanJoin != true; _practice.IsDisabled = choice?.CanJoin != true || !choice.AllowPractice;
        _description.Text = choice == null ? T("No race circuits are configured.") : ClientLanguages.ContentText(choice.Description) + "\n" +
            string.Format(T("{0} laps · {1}–{2} riders · {3} waiting"), choice.Laps, choice.MinimumPlayers, choice.MaximumPlayers, choice.Queued);
        if (_state.Message.Length == 0) _message.Text = T(_state.Phase == MountRacePhase.Ready ? "Confirm your place. The race starts only after every rider is ready." :
            _state.Phase == MountRacePhase.Queued ? "Stay near registration. Closing this window keeps your place in the queue." :
            choice?.CanJoin == true ? "A mount is provided. Everyone has the same speed. Practice gives no prizes." : choice?.Reason ?? "");
    }
    private void Rebuild()
    {
        var scroll = _rows.VerticalScrollBar.ScrollAmount;
        _rows.DeleteAll(); var y = 0;
        if (_showRecords)
        {
            foreach (var reward in _state.Rewards)
            {
                var text = Label(_rows, "Prize" + reward.Id, 44, y + 5, _rows.Width - 220, 28);
                text.Text = string.Format(T("Prize for place {0} · ×{1}"), reward.Place, reward.Quantity);
                var icon = new ImagePanel(_rows); Icon(icon, _state.Presentation.Icons, 4); icon.SetBounds(0, y, 36, 36);
                Button(_rows, "Claim prize", _rows.Width - 170, y, 145, () => Request(MountRaceAction.Claim, reward.Id)); y += 40;
            }
            foreach (var r in _state.Records)
            {
                var text = Label(_rows, "Record" + r.Id, 4, y, _rows.Width - 32, 30);
                text.Text = r.Name + " · " + Time(r.ElapsedMilliseconds) + " · " + T(r.Practice ? "Practice" : "Competition"); y += 32;
            }
            if (y == 0) { Label(_rows, "NoRecords", 0, 0, _rows.Width - 30, 40).Text = T("No records or unclaimed prizes yet."); y = 40; }
        }
        else
        {
            foreach (var r in _state.Riders.OrderBy(r => r.Place > 0 ? 0 : r.Withdrawn ? 2 : 1).ThenBy(r => r.Place).ThenByDescending(r => r.Passed))
            {
                var icon = new ImagePanel(_rows) { MouseInputEnabled = false }; Icon(icon, _state.Presentation.Icons, r.Place > 0 ? 4 : r.Withdrawn ? 5 : r.Ready ? 1 : 2); icon.SetBounds(0, y, 36, 36);
                var text = Label(_rows, "Rider" + r.Id, 44, y + 5, _rows.Width - 76, 28);
                text.Text = r.Name + " — " + (r.Place > 0 ? string.Format(T("Place {0} · {1}"), r.Place, Time(r.ElapsedMilliseconds)) :
                    T(r.Withdrawn ? "Withdrawn" : r.Ready ? "Ready" : "Waiting for confirmation"));
                text.SetToolTipText(text.Text); y += 40;
            }
        }
        _rows.SetInnerSize(_rows.Width - 20, Math.Max(y, _rows.Height - 4)); _rows.VerticalScrollBar.ScrollAmount = scroll; Clock();
    }
    private void Clock()
    {
        var remaining = Math.Max(0, (_state.RemainingMilliseconds - (Environment.TickCount64 - _received) + 999) / 1000);
        _status.Text = _state.Phase switch {
            MountRacePhase.Queued => T("Waiting for riders…"), MountRacePhase.Ready => string.Format(T("Confirm within {0}s · {1}/{2} ready"), remaining, _state.Riders.Count(r => r.Ready), _state.Riders.Length),
            MountRacePhase.Countdown => string.Format(T("Starting in {0}…"), remaining), MountRacePhase.Running => T("Race in progress"),
            MountRacePhase.Finished => T("Race results"), MountRacePhase.Cancelled => T("Race cancelled"), _ => T("Choose a circuit, then enter or practise.") };
        if (Done(_state) && _state.Phase == MountRacePhase.Running) _status.Text = T("Race results");
    }
    private bool Done(MountRaceView view) => view.Phase is MountRacePhase.Finished or MountRacePhase.Cancelled ||
        !view.OnTrack && view.Riders.Any(r => r.Id == PlayerId() && (r.Place > 0 || r.Withdrawn));
    public override void Think() { base.Think(); Clock(); if (!IsHidden && Environment.TickCount64 >= _nextPoll) Request(MountRaceAction.Poll); }
}
