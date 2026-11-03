using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.QualityOfLife;
using DColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Editors;

/// <summary>Edits a detached rules draft; preview and runtime share physical target geometry.</summary>
public sealed class FrmArcheryRangeRules : Form
{
    public ArcheryRules Result { get; }
    private readonly ListBox _rounds = new() { Dock = DockStyle.Top, Height = 136 };
    private readonly ListBox _targets = new() { Dock = DockStyle.Fill };
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 44 };
    private readonly TrackBar _clock = new() { Dock = DockStyle.Bottom, Minimum = 0, Maximum = 600, TickFrequency = 100, Height = 42 };
    private readonly TargetPreview _preview;
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 100 };
    private static string T(string text) => QualityOfLifeText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    private ArcheryRound? SelectedRound => _rounds.SelectedIndex >= 0 && _rounds.SelectedIndex < Result.Rounds.Length ? Result.Rounds[_rounds.SelectedIndex] : null;
    private ArcheryTarget? SelectedTarget => _targets.SelectedIndex >= 0 && SelectedRound is { } round && _targets.SelectedIndex < round.Targets.Length ? round.Targets[_targets.SelectedIndex] : null;
    public FrmArcheryRangeRules(ArcheryRules rules)
    {
        Result = rules.Snapshot(); Text = T("Range rules and targets"); Size = new(1100, 650); MinimumSize = new(760, 520);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        void Button(string name, Action action) { var button = new Button { Text = T(name), AutoSize = true }; button.Click += (_, _) => action(); tools.Controls.Add(button); }
        Button("New round", () => { if (Result.Rounds.Length >= 6) return; Result.Rounds = [..Result.Rounds, new()]; RefreshRounds(Result.Rounds.Length - 1); });
        Button("Delete round", () => { if (Result.Rounds.Length <= 1 || SelectedRound is not { } round) return; Result.Rounds = Result.Rounds.Where(r => r != round).ToArray(); RefreshRounds(0); });
        Button("Round settings", () => _properties.SelectedObject = SelectedRound);
        Button("New target", () => { if (SelectedRound is not { } round || round.Targets.Length >= 8) return; round.Targets = [..round.Targets, new()]; RefreshTargets(round.Targets.Length - 1); });
        Button("Delete target", () => { if (SelectedRound is not { } round || round.Targets.Length <= 1 || SelectedTarget is not { } target) return; round.Targets = round.Targets.Where(t => t != target).ToArray(); RefreshTargets(0); });
        Button("Target settings", () => _properties.SelectedObject = SelectedTarget);
        Button("Save", () => {
            if (!Result.IsValid) { _status.Text = T("Invalid range rules: check distances, timers and target motion bounds."); return; }
            DialogResult = DialogResult.OK; Close();
        });
        Button("Cancel", () => { DialogResult = DialogResult.Cancel; Close(); });
        var animate = new CheckBox { Text = T("Animate preview"), AutoSize = true };
        animate.CheckedChanged += (_, _) => _animation.Enabled = animate.Checked; tools.Controls.Add(animate);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new(1060, 500), SplitterDistance = 170 };
        var right = new SplitContainer { Dock = DockStyle.Fill, Size = new(890, 500), SplitterDistance = 540 };
        right.SizeChanged += (_, _) => {
            if (right.Width >= 400) right.SplitterDistance = Math.Clamp((int)(right.Width * .6), 200, right.Width - 180);
        };
        split.Panel1.Controls.Add(_targets); split.Panel1.Controls.Add(_rounds); split.Panel2.Controls.Add(right);
        _preview = new TargetPreview(this) { Dock = DockStyle.Fill, Name = "ArcheryTargetPreview" };
        right.Panel1.Controls.Add(_preview); right.Panel1.Controls.Add(_clock); right.Panel2.Controls.Add(_properties);
        Controls.Add(split); Controls.Add(tools); Controls.Add(_status);
        _rounds.SelectedIndexChanged += (_, _) => { _properties.SelectedObject = SelectedRound; RefreshTargets(0); };
        _targets.SelectedIndexChanged += (_, _) => { _properties.SelectedObject = SelectedTarget; Preview(); };
        _properties.PropertyValueChanged += (_, _) => Preview(); _clock.ValueChanged += (_, _) => Preview();
        _animation.Tick += (_, _) => _clock.Value = _clock.Value == _clock.Maximum ? 0 : _clock.Value + 1;
        Disposed += (_, _) => { _animation.Stop(); _animation.Dispose(); };
        RefreshRounds(0);
    }
    private void RefreshRounds(int selected)
    {
        _rounds.Items.Clear();
        for (var i = 0; i < Result.Rounds.Length; i++) _rounds.Items.Add(string.Format(T("Round {0}"), i + 1));
        _rounds.SelectedIndex = Math.Clamp(selected, 0, Result.Rounds.Length - 1);
    }
    private void RefreshTargets(int selected)
    {
        _targets.Items.Clear();
        if (SelectedRound is not { } round) return;
        for (var i = 0; i < round.Targets.Length; i++) _targets.Items.Add(string.Format(T("Target {0}"), i + 1));
        if (round.Targets.Length > 0) _targets.SelectedIndex = Math.Clamp(selected, 0, round.Targets.Length - 1);
        Preview();
    }
    private void Preview()
    {
        _preview.Invalidate();
        _status.Text = Result.IsValid ? string.Format(T("Preview {0}s · Maximum {1} points · Drag the selected target. Wind is acceleration in m/s²."),
            _clock.Value / 10d, Result.MaximumScore) : T("Invalid range rules: check distances, timers and target motion bounds.");
    }
    private sealed class TargetPreview(FrmArcheryRangeRules owner) : Control
    {
        private bool _dragging;
        private RectangleF Frame
        {
            get { var scale = Math.Min(Width / 320f, Height / 180f); return new((Width - 320 * scale) / 2, (Height - 180 * scale) / 2, 320 * scale, 180 * scale); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.Clear(DColor.FromArgb(28, 37, 49));
            if (owner.SelectedRound is not { } round || Frame.Width <= 0) return;
            var frame = Frame; var scale = frame.Width / 320;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            using var sky = new SolidBrush(DColor.FromArgb(114, 163, 185)); using var ground = new SolidBrush(DColor.FromArgb(77, 99, 56));
            e.Graphics.FillRectangle(sky, frame); e.Graphics.FillRectangle(ground, frame.X, frame.Y + 96 * scale, frame.Width, 84 * scale);
            for (var index = 0; index < round.Targets.Length; index++)
            {
                var target = round.Targets[index]; var position = target.Position(owner._clock.Value / 10d);
                if (!double.IsFinite(round.Distance) || round.Distance is < 5 or > 100 || !target.IsValid(round.Distance) || !double.IsFinite(position.X) || !double.IsFinite(position.Y)) continue;
                var p = ArcheryPhysics.Project(position.X, position.Y, round.Distance); var radius = (float)(target.Radius * p.Scale * scale);
                if (radius > 5000) continue;
                var x = frame.X + (float)p.X * scale; var y = frame.Y + (float)p.Y * scale;
                using var border = new Pen(index == owner._targets.SelectedIndex ? DColor.Gold : DColor.White, 2);
                e.Graphics.FillEllipse(Brushes.Wheat, x - radius, y - radius, radius * 2, radius * 2);
                e.Graphics.DrawEllipse(border, x - radius, y - radius, radius * 2, radius * 2);
                e.Graphics.FillEllipse(Brushes.IndianRed, x - radius * .35f, y - radius * .35f, radius * .7f, radius * .7f);
                e.Graphics.DrawString((index + 1).ToString(), Font, Brushes.White, x + radius + 3, y - Font.Height / 2f);
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return;
            _dragging = true; Capture = true; Place(e);
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (_dragging) Place(e); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _dragging = false; Capture = false; }
        private void Place(MouseEventArgs e)
        {
            if (owner.SelectedTarget is not { } target || owner.SelectedRound is not { } round || Frame.Width <= 0 || !Frame.Contains(e.Location) ||
                !double.IsFinite(round.Distance) || round.Distance < 5 || !double.IsFinite(target.MotionX) || !double.IsFinite(target.MotionY)) return;
            var frame = Frame; var x = (e.X - frame.X) * 320 / frame.Width; var y = (e.Y - frame.Y) * 180 / frame.Height;
            var position = target.Position(owner._clock.Value / 10d);
            if (!double.IsFinite(position.X) || !double.IsFinite(position.Y)) return;
            var maxX = Math.Max(0, Math.Min(8, round.Distance * .35) - target.MotionX);
            var maxY = Math.Max(0, Math.Min(4, round.Distance * .25) - target.MotionY);
            target.X = Math.Clamp((x - 160) * round.Distance / 200 - (position.X - target.X), -maxX, maxX);
            target.Y = Math.Clamp((84 - y) * round.Distance / 200 - (position.Y - target.Y), -maxY, maxY);
            owner._properties.Refresh(); owner.Preview();
        }
    }
}
