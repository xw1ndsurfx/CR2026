using DarkUI.Forms;
using Intersect.Framework.Core.Cinematics;
using DrawingPoint = System.Drawing.Point;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Cinematics;

internal sealed class FrmCinematicAutoCreator : DarkForm
{
    private sealed record ScenarioChoice(CreatorScenario Value) { public override string ToString() => T("CreatorScenario" + Value); }
    private sealed record ProjectionChoice(CinematicProjection Value) { public override string ToString() => T(Value.ToString()); }
    private readonly CinematicCreationContext _context;
    private readonly TabControl _modes = new() { Dock = DockStyle.Fill };
    private readonly TabControl _steps = new() { Dock = DockStyle.Fill, Multiline = true };
    private readonly TextBox _name = new() { Name = "CreatorName", Dock = DockStyle.Fill, MaxLength = 120 };
    private readonly ComboBox _scenario = new() { Name = "CreatorScenario", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _projection = new() { Name = "CreatorProjection", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _duration = new() { Name = "CreatorDuration", Minimum = 6, Maximum = 600, DecimalPlaces = 2, Value = 12, Width = 120 };
    private readonly NumericUpDown _delay = new() { Name = "CreatorDelay", Minimum = 0, Maximum = 599, DecimalPlaces = 2, Width = 120 };
    private readonly CheckBox _move = new() { Text = T("CreatorMove"), AutoSize = true, Checked = true };
    private readonly ComboBox _npc = new() { Name = "CreatorNpc", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _count = new() { Name = "CreatorCount", Minimum = 1, Maximum = 8, Value = 1, Width = 75 };
    private readonly ListBox _cast = new() { Name = "CreatorCast", Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly NumericUpDown[] _coordinates = Enumerable.Range(0, 6).Select(_ => new NumericUpDown { Minimum = 0, Width = 75 }).ToArray();
    private readonly TextBox _dialogue = new() { Name = "CreatorDialogue", Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, MaxLength = 16000 };
    private readonly ComboBox _music = new() { Name = "CreatorMusic", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _sound = new() { Name = "CreatorSound", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _effect = new() { Name = "CreatorEffect", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _prompt = new() { Name = "CreatorPrompt", Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, MaxLength = 8000 };
    private readonly TextBox _analysis = new() { Name = "CreatorAnalysis", Multiline = true, ReadOnly = true, Dock = DockStyle.Bottom, Height = 120, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _review = new() { Name = "CreatorReview", Multiline = true, ReadOnly = true, TabStop = false, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly CheckBox _partial = new() { Text = T("CreatorPartial"), AutoSize = true, Visible = false };
    private readonly Label _status = new() { Name = "CreatorStatus", Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(6) };
    private readonly Button _generate = new() { Name = "CreatorGenerate", Text = T("CreatorGenerate"), AutoSize = true };
    private readonly Button _accept = new() { Name = "CreatorAccept", Text = T("CreatorOpenTimeline"), AutoSize = true, Enabled = false };
    private readonly Button _play = new() { Text = T("Play"), AutoSize = true, Enabled = false };
    private readonly CinematicTimeline _timeline = new() { Dock = DockStyle.Fill };
    private readonly CreationMap _map;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 50 };
    private CinematicDescriptionResult? _description;
    private bool _ready, _applying, _analyzed, _playing, _requiresPartial;
    private CreatorScenario _lastScenario;
    internal CinematicScene? Result { get; private set; }
    private static string T(string key) => CinematicText.T(key);

    internal FrmCinematicAutoCreator(CinematicCreationContext context, Bitmap? mapImage = null, bool description = false)
    {
        _context = context; _map = new(context, mapImage) { Dock = DockStyle.Fill };
        Text = T("CreatorTitle"); Size = new(1160, 820); MinimumSize = new(1000, 700);
        StartPosition = FormStartPosition.CenterParent; Icon = Intersect.Editor.Core.Program.Icon;
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new(1140, 710), SplitterDistance = 440, FixedPanel = FixedPanel.Panel1,
            Panel1MinSize = 370, Panel2MinSize = 420 };
        var guided = new TabPage(T("CreatorGuided")); guided.Controls.Add(_steps);
        var written = new TabPage(T("CreatorDescription"));
        _modes.TabPages.AddRange([guided, written]); split.Panel1.Controls.Add(_modes);
        var scenarioPage = Step("CreatorStepScenario");
        var scenarioFields = Fields();
        AddField(scenarioFields, "Name", _name); AddField(scenarioFields, "CreatorScenarioLabel", _scenario);
        AddField(scenarioFields, "Projection", _projection);
        AddField(scenarioFields, "CreatorSeconds", _duration); AddField(scenarioFields, "CreatorDelaySeconds", _delay);
        AddField(scenarioFields, "CreatorMovement", _move);
        scenarioPage.Controls.Add(scenarioFields);
        foreach (var kind in Enum.GetValues<CreatorScenario>()) _scenario.Items.Add(new ScenarioChoice(kind));
        foreach (var kind in Enum.GetValues<CinematicProjection>()) _projection.Items.Add(new ProjectionChoice(kind));
        var castPage = Step("CreatorStepCast"); castPage.Controls.Add(_cast);
        var castFields = Fields(); AddField(castFields, "CreatorChooseNpc", _npc); AddField(castFields, "CreatorCopies", _count);
        var castButtons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        castButtons.Controls.Add(Button("CreatorAddCast", () =>
        {
            if (_npc.SelectedItem is not CreatorNpc npc) return;
            if (_cast.Items.Cast<CreatorCast>().Sum(c => c.Count) + _count.Value > 8) { _status.Text = T("CreatorCastLimit"); return; }
            _cast.Items.Add(new CreatorCast(npc, (int)_count.Value)); Changed();
        }));
        castButtons.Controls.Add(Button("Delete", () => { if (_cast.SelectedIndex >= 0) { _cast.Items.RemoveAt(_cast.SelectedIndex); Changed(); } }));
        castPage.Controls.Add(castButtons); castPage.Controls.Add(castFields);
        castPage.Controls.Add(new Label { Text = T("CreatorCastHint"), Dock = DockStyle.Bottom, Height = 58 });
        foreach (var npc in context.Npcs) _npc.Items.Add(npc);
        if (_npc.Items.Count > 0) _npc.SelectedIndex = 0;
        var positionPage = Step("CreatorStepMap"); var positions = Fields();
        for (var i = 0; i < 3; i++)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            for (var axis = 0; axis < 2; axis++)
            {
                var index = i * 2 + axis; _coordinates[index].Name = "CreatorCoordinate" + index;
                _coordinates[index].Maximum = Math.Max(0, (axis == 0 ? context.Width : context.Height) - 1);
                row.Controls.Add(new Label { Text = axis == 0 ? "X" : "Y", AutoSize = true }); row.Controls.Add(_coordinates[index]);
            }
            AddField(positions, i == 0 ? "CreatorEntry" : i == 1 ? "CreatorDestination" : "CreatorCamera", row);
        }
        positionPage.Controls.Add(positions);
        positionPage.Controls.Add(new Label { Text = T("CreatorMapHint"), Dock = DockStyle.Bottom, Height = 72 });
        var dialoguePage = Step("CreatorStepDialogue"); dialoguePage.Controls.Add(_dialogue);
        var ambience = Fields(); AddField(ambience, "Music", _music); AddField(ambience, "Sound", _sound); AddField(ambience, "Animation", _effect);
        dialoguePage.Controls.Add(ambience);
        dialoguePage.Controls.Add(new Label { Text = T("CreatorDialogueHint"), Dock = DockStyle.Top, Height = 42 });
        _music.Items.Add(T("CreatorNone")); _sound.Items.Add(T("CreatorNone"));
        _music.Items.AddRange(context.Music.Cast<object>().ToArray()); _sound.Items.AddRange(context.Sounds.Cast<object>().ToArray());
        _effect.Items.Add(new CreatorEffect(Guid.Empty, T("CreatorNone"))); _effect.Items.AddRange(context.Effects);
        _music.SelectedIndex = _sound.SelectedIndex = _effect.SelectedIndex = 0;
        var reviewPage = Step("CreatorStepReview"); reviewPage.Controls.Add(_review); reviewPage.Controls.Add(_partial);
        _partial.Dock = DockStyle.Bottom;
        written.Controls.Add(_prompt); written.Controls.Add(_analysis);
        var describeActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        describeActions.Controls.Add(Button("CreatorAnalyze", () => AnalyzeDescription()));
        describeActions.Controls.Add(Button("CreatorEditGuide", () => _modes.SelectedIndex = 0));
        written.Controls.Add(describeActions);
        written.Controls.Add(new Label { Text = T("CreatorDescriptionHelp"), Dock = DockStyle.Top, Height = 102 });
        var examples = new TextBox { Text = T("CreatorDescriptionExample"), Multiline = true, ReadOnly = true, Dock = DockStyle.Top, Height = 75 };
        written.Controls.Add(examples);
        var mapTools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        mapTools.Controls.Add(new Label { Text = context.MapName, AutoSize = true, Padding = new Padding(4, 8, 4, 0) });
        for (var i = 0; i < 3; i++)
        {
            var mode = i;
            mapTools.Controls.Add(Button(i == 0 ? "CreatorSetEntry" : i == 1 ? "CreatorSetDestination" : "CreatorSetCamera", () =>
            { _map.Mode = mode; _status.Text = T("CreatorClickMap"); }));
        }
        mapTools.Controls.Add(_play);
        split.Panel2.Controls.Add(_map); split.Panel2.Controls.Add(mapTools);
        var preview = new Panel { Dock = DockStyle.Bottom, Height = 185 };
        preview.Controls.Add(_timeline); preview.Controls.Add(new Label { Text = T("CreatorPreviewTimeline"), Dock = DockStyle.Top, Height = 26 });
        split.Panel2.Controls.Add(preview);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(6) };
        bottom.Controls.Add(Button("CreatorPrevious", () => { _modes.SelectedIndex = 0; _steps.SelectedIndex = Math.Max(0, _steps.SelectedIndex - 1); }));
        bottom.Controls.Add(Button("CreatorNext", () => { _modes.SelectedIndex = 0; _steps.SelectedIndex = Math.Min(4, _steps.SelectedIndex + 1); }));
        bottom.Controls.Add(_generate); bottom.Controls.Add(_accept);
        var cancel = Button("Cancel", () => { DialogResult = DialogResult.Cancel; Close(); }); bottom.Controls.Add(cancel); CancelButton = cancel;
        Controls.Add(split); Controls.Add(_status); Controls.Add(bottom);
        _generate.Click += (_, _) => GeneratePreview();
        _accept.Click += (_, _) =>
        {
            if (Result == null) return;
            try { CinematicAutoCreator.Validate(Result, _context); DialogResult = DialogResult.OK; Close(); }
            catch (InvalidOperationException error) { _status.Text = error.Message; _accept.Enabled = false; }
        };
        _map.Picked += point =>
        { _coordinates[_map.Mode * 2].Value = point.X; _coordinates[_map.Mode * 2 + 1].Value = point.Y; };
        _timeline.Scrubbed += time => { _playing = false; _map.CursorMs = time; _map.Invalidate(); };
        _timeline.Resize += (_, _) => { if (Result != null) _timeline.FitScene(); };
        _timeline.Edited += () =>
        {
            if (Result == null) return;
            try { CinematicAutoCreator.Validate(Result, context); _accept.Enabled = true; _map.Invalidate(); }
            catch (InvalidOperationException error) { _status.Text = error.Message; _accept.Enabled = false; }
        };
        _play.Click += (_, _) => { if (Result != null) { _playing = !_playing; if (_map.CursorMs >= Result.DurationMs) _map.CursorMs = 0; } };
        _timer.Tick += (_, _) =>
        {
            if (!_playing || Result == null) return;
            _map.CursorMs = Math.Min(Result.DurationMs, _map.CursorMs + 50); _timeline.CursorMs = _map.CursorMs;
            _timeline.Invalidate(); _map.Invalidate(); if (_map.CursorMs >= Result.DurationMs) _playing = false;
        };
        _timer.Start();
        _scenario.SelectedIndexChanged += (_, _) =>
        {
            if (_scenario.SelectedItem is not ScenarioChoice choice) return;
            if (_ready && !_applying && _name.Text == T("CreatorScenario" + _lastScenario)) _name.Text = T("CreatorScenario" + choice.Value);
            _lastScenario = choice.Value;
        };
        foreach (var input in Descendants(guided))
        {
            if (input is TextBox { ReadOnly: false } text) text.TextChanged += (_, _) => Changed();
            else if (input is ComboBox combo) combo.SelectedIndexChanged += (_, _) => Changed();
            else if (input is NumericUpDown number) number.ValueChanged += (_, _) => Changed();
            else if (input is CheckBox check) check.CheckedChanged += (_, _) => Changed();
        }
        _partial.CheckedChanged += (_, _) => Changed();
        _prompt.TextChanged += (_, _) => { _analyzed = false; Changed(); UpdateGenerate(); };
        _modes.SelectedIndexChanged += (_, _) =>
        { if (_modes.SelectedIndex == 1 && !_analyzed) { Result = null; _accept.Enabled = false; } UpdateGenerate(); };
        Styling.EditorTheme.Apply(this);
        ApplyPlan(CinematicAutoCreator.Default(context)); _ready = true; Changed();
        _modes.SelectedIndex = description ? 1 : 0;
    }

    private TabPage Step(string key) { var page = new TabPage(T(key)) { Padding = new Padding(8) }; _steps.TabPages.Add(page); return page; }
    private static Button Button(string key, Action action)
    { var button = new Button { Text = T(key), AutoSize = true, Height = 32 }; button.Click += (_, _) => action(); return button; }
    private static TableLayoutPanel Fields() => new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(4) };
    private static void AddField(TableLayoutPanel table, string key, Control control)
    {
        var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.ColumnStyles.Clear(); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        table.Controls.Add(new Label { Text = T(key), AutoSize = true, MaximumSize = new Size(160, 0), Margin = new Padding(3, 8, 8, 8) }, 0, row);
        control.Margin = new Padding(3, 5, 3, 5); table.Controls.Add(control, 1, row);
    }
    private static IEnumerable<Control> Descendants(Control root)
    { foreach (Control child in root.Controls) { yield return child; foreach (var item in Descendants(child)) yield return item; } }
    private CinematicCreationPlan ReadPlan() => new()
    {
        Name = _name.Text, Scenario = (_scenario.SelectedItem as ScenarioChoice)?.Value ?? CreatorScenario.Arrival,
        Projection = (_projection.SelectedItem as ProjectionChoice)?.Value ?? CinematicProjection.Flat2D,
        DurationMs = (int)(_duration.Value * 1000), DelayMs = (int)(_delay.Value * 1000), Move = _move.Checked,
        Entry = new((int)_coordinates[0].Value, (int)_coordinates[1].Value),
        Destination = new((int)_coordinates[2].Value, (int)_coordinates[3].Value),
        Camera = new((int)_coordinates[4].Value, (int)_coordinates[5].Value),
        Cast = _cast.Items.Cast<CreatorCast>().ToList(), Dialogue = _dialogue.Text,
        Music = _music.SelectedIndex <= 0 ? "" : (string)_music.SelectedItem!,
        Sound = _sound.SelectedIndex <= 0 ? "" : (string)_sound.SelectedItem!,
        EffectId = (_effect.SelectedItem as CreatorEffect)?.Id ?? Guid.Empty,
        RequiredNpcCount = _description?.Plan.RequiredNpcCount ?? 0, RequireMusic = _description?.Plan.RequireMusic ?? false,
        RequireEffect = _description?.Plan.RequireEffect ?? false,
    };
    private void ApplyPlan(CinematicCreationPlan plan)
    {
        _applying = true;
        try
        {
            _name.Text = plan.Name; _scenario.SelectedIndex = (int)plan.Scenario; _duration.Value = plan.DurationMs / 1000m; _delay.Value = plan.DelayMs / 1000m;
            _projection.SelectedIndex = (int)plan.Projection;
            _move.Checked = plan.Move; _cast.Items.Clear(); foreach (var cast in plan.Cast) _cast.Items.Add(cast);
            var points = new[] { plan.Entry, plan.Destination, plan.Camera };
            for (var i = 0; i < 3; i++) { _coordinates[i * 2].Value = points[i].X; _coordinates[i * 2 + 1].Value = points[i].Y; }
            _dialogue.Text = plan.Dialogue;
            _music.SelectedIndex = plan.Music.Length == 0 ? 0 : Array.IndexOf(_context.Music, plan.Music) + 1;
            _sound.SelectedIndex = plan.Sound.Length == 0 ? 0 : Array.IndexOf(_context.Sounds, plan.Sound) + 1;
            _effect.SelectedIndex = plan.EffectId == Guid.Empty ? 0 : Array.FindIndex(_context.Effects, e => e.Id == plan.EffectId) + 1;
            _count.Value = Math.Max(1, Math.Min(8, plan.RequiredNpcCount - plan.Cast.Sum(c => c.Count)));
        }
        finally { _applying = false; }
    }
    private void Changed()
    {
        if (!_ready || _applying) return;
        Result = null; _accept.Enabled = _play.Enabled = false; _playing = false; _map.Generated = null;
        _map.Plan = ReadPlan(); _map.Invalidate();
        _timeline.Scene = new() { MapId = _context.MapId, DurationMs = (int)(_duration.Value * 1000) }; _timeline.Invalidate();
        _review.Text = string.Format(T("CreatorPlanSummary"), _name.Text, T("CreatorScenario" + ReadPlan().Scenario),
            _cast.Items.Cast<CreatorCast>().Sum(c => c.Count), _duration.Value) + Environment.NewLine + T("CreatorGenerateHint");
        _status.Text = T("CreatorGenerateHint"); UpdateGenerate();
    }
    private void UpdateGenerate() => _generate.Enabled = _modes.SelectedIndex != 1 || _analyzed;
    internal bool AnalyzeDescription()
    {
        Changed();
        var result = CinematicAutoCreator.Describe(_prompt.Text, _context);
        _analysis.Text = string.Join(Environment.NewLine, result.Errors.Concat(result.Warnings));
        if (result.Errors.Length > 0) { _analyzed = false; _status.Text = result.Errors[0]; UpdateGenerate(); return false; }
        _description = result; _analyzed = true;
        ApplyPlan(result.Plan);
        _requiresPartial = result.Warnings.Contains(T("CreatorUnsupportedActions"));
        _partial.Visible = _requiresPartial; _partial.Checked = false;
        Changed(); _steps.SelectedIndex = result.Plan.RequiredNpcCount > 0 ? 1 : 4;
        _modes.SelectedIndex = 0; _status.Text = T("CreatorDescriptionApplied");
        if (result.Warnings.Length == 0) _analysis.Text = T("CreatorDescriptionApplied");
        return true;
    }
    internal bool GeneratePreview()
    {
        Result = null; _accept.Enabled = _play.Enabled = false; _playing = false; _map.Generated = null;
        _timeline.Scene = new() { DurationMs = (int)(_duration.Value * 1000) }; _timeline.Selected = null; _timeline.Invalidate();
        try
        {
            if (_modes.SelectedIndex == 1 && !_analyzed) throw new InvalidOperationException(T("CreatorAnalyzeFirst"));
            if (_requiresPartial && !_partial.Checked) throw new InvalidOperationException(T("CreatorUnsupportedActions"));
            Result = CinematicAutoCreator.Generate(ReadPlan(), _context);
            _timeline.Scene = Result; _timeline.CursorMs = 0; _timeline.FitScene();
            _map.Generated = Result; _map.CursorMs = 0; _map.Invalidate();
            _review.Text = string.Format(T("CreatorGeneratedSummary"), Result.Actors.Count, Result.Clips.Count, Result.DurationMs / 1000d) +
                Environment.NewLine + T("CreatorPreviewHint") + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, Result.Clips.OrderBy(c => c.StartMs).Select(c =>
                    $"{c.StartMs / 1000d:0.##}s  ·  {c.Name}  ({c.DurationMs / 1000d:0.##}s)"));
            _steps.SelectedIndex = 4; _modes.SelectedIndex = 0; _accept.Enabled = _play.Enabled = true;
            _status.Text = T("CreatorPreviewHint"); return true;
        }
        catch (InvalidOperationException error)
        { _status.Text = error.Message; _review.Text = error.Message; _map.Generated = null; _map.Invalidate(); return false; }
    }
    protected override void Dispose(bool disposing)
    { if (disposing) { _timer.Dispose(); _map.Dispose(); } base.Dispose(disposing); }

    private sealed class CreationMap(CinematicCreationContext context, Bitmap? image) : Control
    {
        internal CinematicCreationPlan? Plan;
        internal CinematicScene? Generated;
        internal int Mode, CursorMs;
        internal event Action<DrawingPoint>? Picked;
        private RectangleF _bounds;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var g = e.Graphics; g.Clear(Styling.EditorTheme.Background);
            var tile = Math.Min(Math.Max(1, Width - 24) / (float)context.Width, Math.Max(1, Height - 32) / (float)context.Height);
            _bounds = new((Width - context.Width * tile) / 2, (Height - context.Height * tile) / 2, context.Width * tile, context.Height * tile);
            if (image != null) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor; g.DrawImage(image, _bounds); }
            else
            {
                using var ground = new SolidBrush(DrawingColor.FromArgb(32, 52, 45)); g.FillRectangle(ground, _bounds);
                using var obstacle = new SolidBrush(DrawingColor.FromArgb(92, 56, 56));
                for (var y = 0; y < context.Height; y++) for (var x = 0; x < context.Width; x++)
                    if (context.Blocked(x, y)) g.FillRectangle(obstacle, _bounds.X + x * tile, _bounds.Y + y * tile, tile, tile);
                g.DrawString(T("CreatorSchematicMap"), Font, Brushes.White, 8, 4);
            }
            using var grid = new Pen(DrawingColor.FromArgb(70, DrawingColor.LightSteelBlue));
            for (var x = 0; x <= context.Width; x++) g.DrawLine(grid, _bounds.X + x * tile, _bounds.Y, _bounds.X + x * tile, _bounds.Bottom);
            for (var y = 0; y <= context.Height; y++) g.DrawLine(grid, _bounds.X, _bounds.Y + y * tile, _bounds.Right, _bounds.Y + y * tile);
            PointF Screen(float x, float y) => new(_bounds.X + (x + .5f) * tile, _bounds.Y + (y + .5f) * tile);
            void Marker(DrawingPoint point, string text, Brush brush, int offset)
            {
                var p = Screen(point.X, point.Y);
                if (brush == Brushes.DeepSkyBlue) { using var pen = new Pen(brush, 2); g.DrawRectangle(pen, p.X - 8, p.Y - 8, 16, 16); }
                else g.FillEllipse(brush, p.X - 6, p.Y - 6, 12, 12);
                g.DrawString(text, Font, Brushes.White, p.X + 8, p.Y + offset);
            }
            if (Plan is { } plan)
            {
                Marker(plan.Entry, T("CreatorEntry"), Brushes.LimeGreen, -18);
                Marker(plan.Destination, T("CreatorDestination"), Brushes.Gold, -18);
                Marker(plan.Camera, T("CreatorCamera"), Brushes.DeepSkyBlue, -34);
            }
            if (Generated is { } scene)
            {
                using var path = new Pen(DrawingColor.DeepSkyBlue, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                foreach (var clip in scene.Clips.Where(c => c.Track == CinematicTrack.Movement))
                    for (var i = 1; i < clip.Points.Count; i++)
                        g.DrawLine(path, Screen(clip.Points[i - 1].X, clip.Points[i - 1].Y), Screen(clip.Points[i].X, clip.Points[i].Y));
                foreach (var actor in scene.Actors.Where(a => CinematicPreview.ActorVisible(scene, a, CursorMs)))
                { var p = CinematicPreview.ActorPosition(scene, actor, CursorMs); var screen = Screen(p.X, p.Y); g.FillEllipse(Brushes.White, screen.X - 4, screen.Y - 4, 8, 8); g.DrawString(actor.Name, Font, Brushes.White, screen.X + 7, screen.Y + 6); }
                var dialogue = scene.Clips.FirstOrDefault(c => c.Track == CinematicTrack.Dialogue && CursorMs >= c.StartMs && CursorMs < c.StartMs + c.DurationMs);
                if (dialogue != null)
                {
                    using var background = new SolidBrush(DrawingColor.FromArgb(225, 15, 22, 30));
                    g.FillRectangle(background, 10, Math.Max(0, Height - 70), Math.Max(1, Width - 20), 60);
                    g.DrawString(dialogue.Text, Font, Brushes.White, new RectangleF(18, Math.Max(0, Height - 63), Math.Max(1, Width - 36), 50));
                }
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); if (e.Button != MouseButtons.Left || !_bounds.Contains(e.Location)) return;
            Picked?.Invoke(new(Math.Min(context.Width - 1, (int)((e.X - _bounds.X) / _bounds.Width * context.Width)),
                Math.Min(context.Height - 1, (int)((e.Y - _bounds.Y) / _bounds.Height * context.Height))));
        }
        protected override void Dispose(bool disposing) { if (disposing) image?.Dispose(); base.Dispose(disposing); }
    }
}
