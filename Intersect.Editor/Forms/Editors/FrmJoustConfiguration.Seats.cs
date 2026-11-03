using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.GameObjects.NPCs;
namespace Intersect.Editor.Forms.Editors;
public sealed partial class FrmJoustConfiguration
{
    private sealed record SeatChoice(JoustSeat Seat) { public override string ToString() => Seat.Name.Length > 0 ? Seat.Name : $"({Seat.X}, {Seat.Y})"; }
    private readonly ComboBox _seat = new() { Name = "JoustSeat", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _crowd = new() { Name = "JoustCrowdNpc", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _facing = new() { Name = "JoustSeatFacing", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _seatName = new() { Name = "JoustSeatName", Dock = DockStyle.Fill, MaxLength = 64 };
    private readonly Label _seatCoordinates = new() { AutoSize = true };
    private readonly NumericUpDown _spectatorZoom = new() { Name = "JoustSpectatorZoom", Minimum = .5m, Maximum = 3, DecimalPlaces = 2, Increment = .05m, Dock = DockStyle.Fill };
    private JoustSeat? SelectedSeat => (_seat.SelectedItem as SeatChoice)?.Seat;
    private void BuildSeats(TabControl tabs)
    {
        var page = Page(tabs, "6. Seats");
        Row(page, "Grandstand seat", _seat); Row(page, "Seat name", _seatName);
        var tools = new FlowLayoutPanel { AutoSize = true };
        AddButton(tools, "Add seat", () =>
        {
            if (Arena is not { } a || a.Seats.Length >= 64) return;
            // The first free adjacent pair outside the lanes is a safe editable starting point.
            for (var y = 0; y < Intersect.Options.Instance.Map.MapHeight - 1; y++)
            for (var x = 0; x < Intersect.Options.Instance.Map.MapWidth; x++)
            {
                if (a.OnTrack(x, y) || a.OnTrack(x, y + 1) || a.Seats.Any(s => s.X == x && (s.Y == y || s.Y == y + 1) || s.ExitX == x && s.ExitY == y)) continue;
                var seat = new JoustSeat { X = x, Y = y, ExitX = x, ExitY = y + 1 };
                a.Seats = [..a.Seats, seat]; LoadSeats(seat.Id); Changed(); Preview(); return;
            }
        });
        AddButton(tools, "Remove seat", () => { if (Arena is not { } a || SelectedSeat is not { } s) return; a.Seats = a.Seats.Where(x => x != s).ToArray(); LoadSeats(); Changed(); Preview(); });
        Row(page, "", tools);
        var placement = new FlowLayoutPanel { AutoSize = true };
        AddButton(placement, "Place seat", () => SetPlacement(Placement.Seat));
        AddButton(placement, "Place seat entrance", () => SetPlacement(Placement.SeatExit)); Row(page, "", placement);
        var adjacent = new FlowLayoutPanel { AutoSize = true };
        AddButton(adjacent, "Put entrance beside seat", PutEntranceBesideSeat); Row(page, "", adjacent);
        Row(page, "Track positions", _seatCoordinates);
        _facing.Items.AddRange(new object[] { T("Up"), T("Down"), T("Left"), T("Right") }); Row(page, "Facing", _facing);
        Row(page, "Crowd NPC (optional)", _crowd);
        Row(page, "Spectator zoom", _spectatorZoom);
        _spectatorZoom.ValueChanged += (_, _) => { if (!_loading && Arena is { } a) { a.SpectatorZoom = (float)_spectatorZoom.Value; Changed(); } };
        Row(page, "How to play", new Label { AutoSize = true, MaximumSize = new(320, 0), Text = T("Add a seat, then place its bench tile and an adjacent entrance on the map. Keep both outside the tracks. Players stand at the entrance and choose the seat in Royal Jousting. An optional NPC fills empty seats. Existing chat emotes remain available while seated.") });
        _seat.SelectedIndexChanged += (_, _) => LoadSeatFields();
        _seatName.TextChanged += (_, _) => { if (_loading || SelectedSeat is not { } s) return; s.Name = _seatName.Text; var i = _seat.SelectedIndex; _seat.Items[i] = new SeatChoice(s); Changed(); };
        _facing.SelectedIndexChanged += (_, _) => { if (_loading || SelectedSeat is not { } s) return; s.Facing = _facing.SelectedIndex; Changed(); };
        _crowd.SelectedIndexChanged += (_, _) => { if (_loading || Arena is not { } a || _crowd.SelectedItem is not Choice c) return; a.CrowdNpcId = c.Id; Changed(); };
    }
    private void PutEntranceBesideSeat()
    {
        if (SelectedSeat is not { } seat || Arena is not { } a || General.Globals.CurrentMap is not { } map || map.Id != a.MapId) return;
        foreach (var (dx, dy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
        {
            var x = seat.X + dx; var y = seat.Y + dy;
            if (x < 0 || y < 0 || x >= Intersect.Options.Instance.Map.MapWidth || y >= Intersect.Options.Instance.Map.MapHeight || a.OnTrack(x,y) || a.Seats.Any(s => s.X == x && s.Y == y) || map.Attributes[x,y] != null) continue;
            seat.ExitX = x; seat.ExitY = y; LoadSeatFields(); Changed(); Preview(); return;
        }
        _status.Text = T("No adjacent entrance is free. Move the bench or clear a neighboring tile.");
    }
    private void LoadSeats(Guid selected = default)
    {
        var loading = _loading; _loading = true;
        _seat.Items.Clear(); _seat.Items.AddRange((Arena?.Seats ?? []).Select(s => (object)new SeatChoice(s)).ToArray());
        _seat.SelectedIndex = _seat.Items.Count == 0 ? -1 : Math.Max(0, Array.FindIndex(Arena!.Seats, s => s.Id == selected));
        _crowd.Items.Clear(); _crowd.Items.Add(new Choice(Guid.Empty, T("None (optional)")));
        _crowd.Items.AddRange(NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>().OrderBy(n => n.Name).Select(n => (object)new Choice(n.Id, n.Name)).ToArray());
        var id = Arena?.CrowdNpcId ?? Guid.Empty;
        if (id != Guid.Empty && !_crowd.Items.Cast<Choice>().Any(c => c.Id == id)) _crowd.Items.Add(new Choice(id, T("Missing crowd NPC")));
        _crowd.SelectedIndex = Enumerable.Range(0, _crowd.Items.Count).First(i => ((Choice)_crowd.Items[i]).Id == id);
        _spectatorZoom.Value = Math.Clamp((decimal)(Arena?.SpectatorZoom ?? 1.25f), _spectatorZoom.Minimum, _spectatorZoom.Maximum);
        LoadSeatFields(); _loading = loading;
    }
    private void LoadSeatFields()
    {
        var loading = _loading; _loading = true;
        _seatName.Text = SelectedSeat?.Name ?? ""; _facing.SelectedIndex = SelectedSeat?.Facing ?? -1;
        var s = SelectedSeat;
        _seatCoordinates.Text = s == null ? T("Select a seat to place it.") : $"{T("Grandstand seat")}: ({s.X}, {s.Y})\n{T("Seat entrance")}: ({s.ExitX}, {s.ExitY})";
        _loading = loading;
    }
}
