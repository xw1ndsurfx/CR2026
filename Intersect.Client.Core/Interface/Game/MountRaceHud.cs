using System.Numerics;
using Intersect.Client.Core;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Maps;
using Intersect.Configuration;
using Intersect.Framework.Core.MountRacing;
using Rect = Intersect.Client.Framework.GenericClasses.Rectangle;

namespace Intersect.Client.Interface.Game;

internal sealed class MountRaceHud : Base
{
    private readonly Canvas _canvas;
    private readonly Label _title, _progress;
    private readonly ImagePanel _marker;
    private readonly Label _markerLabel;
    private MountRaceView _state = new();
    private long _received;
    private int _lastCountdown = -1;
    internal Func<Guid> PlayerId { get; set; } = () => Globals.Me?.Id ?? Guid.Empty;
    internal MountRaceHud(Canvas canvas, Action details, Action leave) : base(canvas, nameof(MountRaceHud))
    {
        _canvas = canvas; MouseInputEnabled = false; KeyboardInputEnabled = false;
        SetBounds(Math.Max(0, (canvas.Width - 400) / 2), 12, Math.Min(400, canvas.Width), 126);
        _title = MountRaceWindow.Label(this, "RaceClock", 12, 8, Width - 24, 26);
        _progress = MountRaceWindow.Label(this, "RaceProgress", 12, 36, Width - 24, 42);
        _progress.WrappingBehavior = WrappingBehavior.Wrapped;
        MountRaceWindow.Button(this, "Race details", 12, 82, (Width - 32) / 2, details);
        MountRaceWindow.Button(this, "Leave race", 20 + (Width - 32) / 2, 82, (Width - 32) / 2, leave);
        _marker = new ImagePanel(canvas, "RaceCheckpointMarker") { MouseInputEnabled = false, KeyboardInputEnabled = false };
        _marker.SetSize(52, 52); _marker.SendToBack();
        _markerLabel = MountRaceWindow.Label(canvas, "RaceCheckpointNumber", 0, 0, 60, 24); _markerLabel.SendToBack();
        Apply(new());
    }
    internal void Apply(MountRaceView view)
    {
        var previous = _state; var before = previous.Riders.FirstOrDefault(r => r.Id == PlayerId());
        _state = view; _received = Environment.TickCount64;
        var current = view.Riders.FirstOrDefault(r => r.Id == PlayerId());
        if (view.RaceId != previous.RaceId) _lastCountdown = -1;
        if (view.OnTrack && view.Phase == MountRacePhase.Running && previous.Phase == MountRacePhase.Countdown) MountRaceWindow.Play(view.Presentation.StartSound);
        else if (view.OnTrack && before != null && current != null && current.Passed > before.Passed && current.Place == 0)
            MountRaceWindow.Play(view.Checkpoints.Length > 0 && current.Passed % view.Checkpoints.Length == 0 ? view.Presentation.LapSound : view.Presentation.CheckpointSound);
        MountRaceWindow.Icon(_marker, view.Presentation.Icons, 3);
        IsHidden = !view.OnTrack;
        Refresh();
    }
    private void Refresh()
    {
        SetPosition(Math.Max(0, (_canvas.Width - Width) / 2), 12);
        var elapsed = Math.Max(0, Environment.TickCount64 - _received);
        var seconds = (int)Math.Max(0, (_state.RemainingMilliseconds - elapsed + 999) / 1000);
        var rider = _state.Riders.FirstOrDefault(r => r.Id == PlayerId());
        _title.Text = _state.Phase == MountRacePhase.Countdown ? string.Format(MountRaceWindow.T("Starting in {0}…"), seconds) :
            ClientLanguages.ContentText(_state.CourseName) + " · " + MountRaceWindow.Time(rider?.Place > 0 ? rider.ElapsedMilliseconds : _state.ElapsedMilliseconds + elapsed);
        _progress.Text = _state.Checkpoints.Length == 0 || rider == null ? "" :
            string.Format(MountRaceWindow.T("Lap {0}/{1} · Checkpoint {2}/{3} · {4}s left"),
                Math.Min(_state.Laps, rider.Passed / _state.Checkpoints.Length + 1), _state.Laps,
                rider.Passed % _state.Checkpoints.Length + 1, _state.Checkpoints.Length, seconds);
        if (rider?.Place > 0) _progress.Text = string.Format(MountRaceWindow.T("Place {0} · {1}"), rider.Place, MountRaceWindow.Time(rider.ElapsedMilliseconds));
        if (_state.OnTrack && _state.Phase == MountRacePhase.Countdown && seconds > 0 && seconds != _lastCountdown)
        { _lastCountdown = seconds; MountRaceWindow.Play(_state.Presentation.TickSound); }
        _marker.IsHidden = _markerLabel.IsHidden = true;
        if (!_state.OnTrack || _state.Phase != MountRacePhase.Running || rider is not { Place: 0, Withdrawn: false } || _state.Checkpoints.Length == 0 ||
            MapInstance.Get(_state.MapId) is not { IsLoaded: true } map) return;
        var target = _state.Checkpoints[rider.Passed % _state.Checkpoints.Length];
        var point = TutorialWorldMarker.ProjectPoint(new Vector2(map.X + (target.X + .5f) * Options.Instance.Map.TileWidth,
            map.Y + (target.Y + .5f) * Options.Instance.Map.TileHeight), new Vector2(Graphics.CurrentView.Left, Graphics.CurrentView.Top), Graphics.WorldScale, _canvas.Scale);
        var x = (int)Math.Clamp(point.X - 26, 8, Math.Max(8, _canvas.Width - 60));
        var y = (int)Math.Clamp(point.Y - 64, 145, Math.Max(145, _canvas.Height - 90));
        _marker.SetPosition(x, y); _markerLabel.SetPosition(x, y + 50);
        _markerLabel.Text = (rider.Passed % _state.Checkpoints.Length + 1) + "/" + _state.Checkpoints.Length;
        _marker.IsHidden = _markerLabel.IsHidden = false;
    }
    protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
    {
        skin.Renderer.DrawColor = new Color(225, 36, 24, 16); skin.Renderer.DrawFilledRect(new Rect(0, 0, Width, Height));
        skin.Renderer.DrawColor = new Color(255, 189, 145, 60); skin.Renderer.DrawFilledRect(new Rect(0, Height - 2, Width, 2));
    }
    public override void Think() { base.Think(); Refresh(); }
}
