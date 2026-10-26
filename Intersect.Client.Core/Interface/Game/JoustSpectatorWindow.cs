using System.Text.Json;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Jousting;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
internal sealed class JoustSpectatorWindow : Window
{
    private readonly ComboBox _seats;
    private readonly Label _score, _message, _entrance;
    private readonly Button _sit, _stand;
    private JoustSpectatorView _state = new();
    private JoustSeatView[] _displayedSeats = [];
    private readonly JoustViewport _viewport;
    private readonly List<(Base Control, int Y)> _layout = [];
    private (Guid Match, int Pass)? _presentationKey;
    internal JoustSpectatorWindow(Canvas canvas) : base(canvas, ClientLanguages.Text("Joust grandstands"), false, nameof(JoustSpectatorWindow))
    {
        SetSize(660, 360); Alignment = [Alignments.Center]; DisableResizing(); DeleteOnClose = false;
        T Track<T>(T control) where T : Base { _layout.Add((control, control.Y)); return control; }
        Label Text(int y, int height)
        {
            var label = new Label(this) { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11, TextColorOverride = Color.White, AutoSizeToContents = false };
            label.SetBounds(20, y, 620, height); if (y >= 104) Track(label); return label;
        }
        _score = Text(42, 54);
        _seats = new ComboBox(this, "JoustSeatChoice") { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11 };
        _seats.SetBounds(20, 104, 470, 30); _seats.ItemSelected += (_, _) => Selection();
        Track(_seats);
        Track(NavalManagementWindow.Button(this, "JoustSeatsRefresh", ClientLanguages.Text("Refresh"), 506, 104, 134, 30, () => Send(JoustAction.BrowseSeats)));
        _entrance = Text(142, 34);
        _sit = NavalManagementWindow.Button(this, "JoustSit", ClientLanguages.Text("Sit in this seat"), 20, 182, 300, 30, () => Send(JoustAction.ReserveSeat));
        _stand = NavalManagementWindow.Button(this, "JoustStand", ClientLanguages.Text("Stand up"), 340, 182, 300, 30, () => Send(JoustAction.LeaveSeat));
        Track(_sit); Track(_stand);
        var cameraNames = new[] { "Arena camera", "Follow gold knight", "Follow blue knight" };
        for (var i = 0; i < 3; i++)
        {
            var camera = i;
            Track(NavalManagementWindow.Button(this, "JoustCamera" + i, ClientLanguages.Text(cameraNames[i]), 20 + i * 210, 228, 200, 30, () => Send(JoustAction.SpectatorCamera, camera)));
        }
        _message = Text(276, 56); Hide();
        _message.WrappingBehavior = WrappingBehavior.Wrapped;
        _viewport = new JoustViewport(this) { ReadOnly = true, MouseInputEnabled = false, KeyboardInputEnabled = false };
        _viewport.SetBounds(20, 104, 620, 320); _viewport.Hide();
    }
    protected override void EnsureInitialized() { }
    private void Send(JoustAction action, int camera = 0) => Intersect.Client.Networking.Network.SendPacket(new JoustRequestPacket { Action = action, SeatId = _seats.SelectedItem?.UserData as Guid? ?? Guid.Empty, Camera = camera });
    private void Selection()
    {
        var id = _seats.SelectedItem?.UserData as Guid?;
        var seat = _state.Seats.FirstOrDefault(s => s.Id == id);
        _entrance.Text = seat == null ? ClientLanguages.Text("No grandstand seats are configured here.") : ClientLanguages.Format("Seat entrance: ({0}, {1})", seat.ExitX, seat.ExitY);
        _sit.IsDisabled = seat == null || seat.Occupied || _state.CurrentSeat != Guid.Empty;
        _stand.IsDisabled = _state.CurrentSeat == Guid.Empty;
    }
    internal void Apply(JoustStatePacket packet)
    {
        if (packet.Message.Length > 0 && !IsHidden) _message.Text = ClientLanguages.Text(packet.Message);
        if (packet.SpectatorJson.Length == 0) return;
        var previousSeat = _state.CurrentSeat;
        try { _state = JsonSerializer.Deserialize<JoustSpectatorView>(packet.SpectatorJson) ?? new(); } catch (JsonException) { return; }
        if (previousSeat != _state.CurrentSeat) _message.Text = "";
        if (!_displayedSeats.SequenceEqual(_state.Seats))
        {
            var selected = _seats.SelectedItem?.UserData as Guid? ?? _state.CurrentSeat;
            _seats.ClearItems();
            foreach (var seat in _state.Seats)
                _seats.AddItem((seat.Name.Length > 0 ? ClientLanguages.ContentText(seat.Name) : ClientLanguages.Format("Seat ({0}, {1})", seat.X, seat.Y)) + (seat.Occupied ? " · " + seat.Occupant : ""), userData: seat.Id);
            _seats.SelectByUserData(selected);
            if (_seats.SelectedItem == null && _state.Seats.Length > 0) _seats.SelectByUserData(_state.Seats[0].Id);
            _displayedSeats = _state.Seats;
        }
        Selection();
        var match = _state.Match;
        _score.Text = match == null ? ClientLanguages.Text("Waiting for the next joust.") : ClientLanguages.Format("Pass {0}/{1} · {2} {3} : {4} {5}", match.Pass, match.Passes, match.FirstName, match.FirstScore, match.SecondScore, string.IsNullOrEmpty(match.SecondName) ? ClientLanguages.Text("AI") : match.SecondName) + "\n" + ClientLanguages.Text(match.Live ? "Joust in progress" : "Joust finished");
        var presentation = _state.CurrentSeat != Guid.Empty && _state.Presentation is { } p && p.MatchId == match?.MatchId &&
            p.Phase is JoustPhase.Charge or JoustPhase.Aim or JoustPhase.Impact or JoustPhase.Results ? p : null;
        _viewport.Apply(presentation);
        var showing = presentation != null;
        SetSize(660, showing ? 704 : 360);
        foreach (var (control, y) in _layout) control.SetPosition(control.X, y + (showing ? 344 : 0));
        if (presentation != null)
        {
            _viewport.Show();
            _message.Text = ClientLanguages.Text("Spectator view is read only. Use the gold or blue camera button to choose the knight's viewpoint.");
            var key = (presentation.MatchId, presentation.Pass);
            if (_presentationKey != key) { _presentationKey = key; Show(); BringToFront(); }
        }
        else { _viewport.Hide(); if (_state.CurrentSeat == Guid.Empty || match == null || !match.Live) _presentationKey = null; }
        if (packet.OpenSpectatorWindow) { Show(); BringToFront(); }
    }
}
