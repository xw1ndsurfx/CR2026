using System.Collections;
using DarkUI.Forms;
using Intersect.Editor.Core;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.PlayerClass;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms;

public sealed class FrmBalanceLab : DarkForm
{
    public enum BalanceObjectKind
    {
        Npc,
        Item,
        Spell,
        Resource,
        PlayerClass,
    }

    public sealed class BalanceOpenRequest
    {
        public required BalanceObjectKind Kind { get; init; }

        public Guid Id { get; init; }
    }

    private sealed class ClassChoice
    {
        public Guid? Id { get; init; }

        public required string Name { get; init; }

        public override string ToString() => Name;
    }

    private sealed class CombatSimulation
    {
        public int Level { get; init; }

        public required string ClassName { get; init; }

        public double PlayerHp { get; init; }

        public double PlayerMana { get; init; }

        public double PlayerDamagePerHit { get; init; }

        public double PlayerAttackSeconds { get; init; }

        public double PlayerDps { get; init; }

        public double AutoAttackDps { get; init; }

        public double SpellDps { get; init; }

        public double ManaUsePerSecond { get; init; }

        public double GearPower { get; init; }

        public string GearSummary { get; init; } = string.Empty;

        public string SpellSummary { get; init; } = string.Empty;

        public double NpcDamagePerHit { get; init; }

        public double NpcAttackSeconds { get; init; }

        public double NpcDps { get; init; }

        public double TtkSeconds { get; init; }

        public double HpLossPercent { get; init; }

        public double TimeToPartyWipeSeconds { get; init; }
    }

    private sealed class LoadoutSnapshot
    {
        public double[] FlatStats { get; } = new double[5];

        public double[] PercentStats { get; } = new double[5];

        public double[] FlatVitals { get; } = new double[2];

        public double[] PercentVitals { get; } = new double[2];

        public double[] VitalRegen { get; } = new double[2];

        public object? Weapon { get; set; }

        public double Power { get; set; }

        public int ItemCount { get; set; }

        public string Summary { get; set; } = "No equipment";
    }

    private sealed class SpellRotationSnapshot
    {
        public double Dps { get; set; }

        public double ManaPerSecond { get; set; }

        public double CastOccupancy { get; set; }

        public int SpellCount { get; set; }

        public string Summary { get; set; } = "Class spells disabled";
    }

    private sealed class ProgressionPoint
    {
        public int Level { get; init; }

        public double Ttk { get; init; }

        public double HpLoss { get; init; }

        public double Dps { get; init; }
    }

    private sealed class ProgressionChart : Control
    {
        private readonly IReadOnlyList<ProgressionPoint> _points;
        private readonly Func<ProgressionPoint, double> _selector;
        private readonly double _target;
        private readonly string _title;
        private readonly string _suffix;

        public ProgressionChart(
            IReadOnlyList<ProgressionPoint> points,
            Func<ProgressionPoint, double> selector,
            double target,
            string title,
            string suffix
        )
        {
            _points = points;
            _selector = selector;
            _target = target;
            _title = title;
            _suffix = suffix;

            Dock = DockStyle.Fill;
            DoubleBuffered = true;
            BackColor = System.Drawing.Color.FromArgb(24, 21, 22);
            ForeColor = System.Drawing.Color.Gainsboro;
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var graphics = e.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var titleBrush = new SolidBrush(System.Drawing.Color.FromArgb(247, 69, 96));
            using var textBrush = new SolidBrush(System.Drawing.Color.Gainsboro);
            using var gridPen = new Pen(System.Drawing.Color.FromArgb(58, 50, 52));
            using var linePen = new Pen(System.Drawing.Color.FromArgb(120, 205, 255), 2f);
            using var targetPen = new Pen(System.Drawing.Color.FromArgb(255, 205, 120), 1.5f)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash,
            };

            graphics.DrawString(_title, new Font(Font, FontStyle.Bold), titleBrush, 10, 8);

            if (_points.Count == 0 || Width < 120 || Height < 100)
            {
                graphics.DrawString("No progression data.", Font, textBrush, 10, 36);
                return;
            }

            var plot = new Rectangle(58, 38, Math.Max(20, Width - 78), Math.Max(20, Height - 70));
            var values = _points.Select(_selector).Where(double.IsFinite).ToArray();
            if (values.Length == 0)
            {
                return;
            }

            var maxValue = Math.Max(values.Max(), _target);
            maxValue = Math.Max(1d, maxValue * 1.10d);

            for (var i = 0; i <= 4; i++)
            {
                var y = plot.Top + plot.Height * i / 4f;
                graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);

                var value = maxValue * (1d - i / 4d);
                graphics.DrawString(
                    $"{value:0.#}{_suffix}",
                    Font,
                    textBrush,
                    4,
                    y - Font.Height / 2f
                );
            }

            graphics.DrawRectangle(gridPen, plot);

            float XFor(int index) =>
                _points.Count <= 1
                    ? plot.Left
                    : plot.Left + plot.Width * index / (float)(_points.Count - 1);

            float YFor(double value) =>
                plot.Bottom - (float)(Math.Clamp(value, 0d, maxValue) / maxValue * plot.Height);

            if (_target > 0)
            {
                var targetY = YFor(_target);
                graphics.DrawLine(targetPen, plot.Left, targetY, plot.Right, targetY);
                graphics.DrawString(
                    $"Target {_target:0.#}{_suffix}",
                    Font,
                    textBrush,
                    Math.Max(plot.Left, plot.Right - 125),
                    Math.Max(plot.Top, targetY - Font.Height - 2)
                );
            }

            for (var i = 1; i < _points.Count; i++)
            {
                var previous = _selector(_points[i - 1]);
                var current = _selector(_points[i]);
                if (!double.IsFinite(previous) || !double.IsFinite(current))
                {
                    continue;
                }

                graphics.DrawLine(
                    linePen,
                    XFor(i - 1),
                    YFor(previous),
                    XFor(i),
                    YFor(current)
                );
            }

            var labelLevels = new[]
            {
                0,
                Math.Max(0, (_points.Count - 1) / 4),
                Math.Max(0, (_points.Count - 1) / 2),
                Math.Max(0, (_points.Count - 1) * 3 / 4),
                _points.Count - 1,
            }.Distinct();

            foreach (var index in labelLevels)
            {
                var x = XFor(index);
                var label = $"Lv {_points[index].Level}";
                graphics.DrawString(label, Font, textBrush, x - 18, plot.Bottom + 5);
            }
        }
    }

    private sealed class ProgressionForm : DarkForm
    {
        public ProgressionForm(
            string npcName,
            string simulationLabel,
            IReadOnlyList<ProgressionPoint> points,
            double targetTtk,
            double targetHpLoss
        )
        {
            Text = $"Balance Progression - {npcName}";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 650);
            Size = new Size(1180, 780);
            BackColor = System.Drawing.Color.FromArgb(18, 18, 18);
            ForeColor = System.Drawing.Color.Gainsboro;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = new Padding(10),
                BackColor = System.Drawing.Color.FromArgb(18, 18, 18),
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            Controls.Add(root);

            var header = new Label
            {
                Dock = DockStyle.Fill,
                Text = $"{npcName}   |   {simulationLabel}   |   Level 1 -> {points.LastOrDefault()?.Level ?? 1}",
                ForeColor = System.Drawing.Color.Gainsboro,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
            };
            root.Controls.Add(header, 0, 0);

            root.Controls.Add(
                new ProgressionChart(
                    points,
                    point => point.Ttk,
                    targetTtk,
                    "TIME TO KILL BY PLAYER LEVEL",
                    "s"
                ),
                0,
                1
            );

            root.Controls.Add(
                new ProgressionChart(
                    points,
                    point => point.HpLoss,
                    targetHpLoss,
                    "ESTIMATED PARTY HP LOST BY PLAYER LEVEL",
                    "%"
                ),
                0,
                2
            );
        }
    }

    private sealed class BalanceEntry
    {
        public required BalanceObjectKind Kind { get; init; }

        public Guid Id { get; init; }

        public required string Name { get; init; }

        public required string Group { get; init; }

        public double Power { get; init; }

        public double Reward { get; init; }

        public double Baseline { get; set; }

        public double DeviationPercent =>
            Baseline <= 0.0001 ? 0 : (Power / Baseline - 1d) * 100d;

        public string Severity { get; set; } = "OK";

        public required string Metrics { get; init; }

        public string Suggestion { get; set; } = string.Empty;

        public double? SimulationTtk { get; set; }

        public double? SimulationHpLoss { get; set; }

        public double? SimulationPlayerDps { get; set; }

        public double? SimulationNpcDps { get; set; }

        public double? SimulationSuggestedHp { get; set; }

        public double? SimulationSuggestedDamage { get; set; }

        public int? SimulationLevel { get; set; }

        public string SimulationClass { get; set; } = string.Empty;

        public string SimulationStatus { get; set; } = string.Empty;

        public string SimulationNotes { get; set; } = string.Empty;
    }

    private readonly Action<BalanceOpenRequest>? _openEditor;

    private readonly ComboBox _profile = new();
    private readonly NumericUpDown _warningThreshold = new();
    private readonly NumericUpDown _criticalThreshold = new();
    private readonly ListBox _scope = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _details = new();
    private readonly Label _summary = new();

    private readonly ComboBox _simulationClass = new();
    private readonly CheckBox _matchNpcLevel = new();
    private readonly NumericUpDown _simulationLevel = new();
    private readonly NumericUpDown _partySize = new();
    private readonly NumericUpDown _targetTtk = new();
    private readonly NumericUpDown _targetHpLoss = new();
    private readonly ComboBox _gearProfile = new();
    private readonly CheckBox _includeClassSpells = new();

    private readonly List<ClassChoice> _simulationClasses = new();
    private readonly List<BalanceEntry> _entries = new();

    public FrmBalanceLab(Action<BalanceOpenRequest>? openEditor = null)
    {
        _openEditor = openEditor;

        Text = "Corps Royaux - Game Balance Lab";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 720);
        Size = new Size(1500, 900);
        BackColor = System.Drawing.Color.FromArgb(18, 18, 18);
        ForeColor = System.Drawing.Color.Gainsboro;

        BuildInterface();

        Shown += (_, _) =>
        {
            LoadSimulationClasses();
            _profile.SelectedIndex = 1;
            RunAnalysis();
        };
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = System.Drawing.Color.FromArgb(18, 18, 18),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        var topBars = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
        };
        topBars.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topBars.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        topBars.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        topBars.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.Controls.Add(topBars, 0, 0);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10, 10, 10, 8),
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
        };

        toolbar.Controls.Add(CreateToolbarLabel("Profile:"));

        _profile.Width = 150;
        _profile.DropDownStyle = ComboBoxStyle.DropDownList;
        _profile.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _profile.ForeColor = System.Drawing.Color.White;
        _profile.Items.AddRange(new object[] { "Conservative", "Standard", "Strict" });
        _profile.SelectedIndexChanged += (_, _) => ApplyProfile();
        toolbar.Controls.Add(_profile);

        toolbar.Controls.Add(CreateToolbarLabel("Warning %:"));
        ConfigureThreshold(_warningThreshold, 5, 100, 20);
        toolbar.Controls.Add(_warningThreshold);

        toolbar.Controls.Add(CreateToolbarLabel("Critical %:"));
        ConfigureThreshold(_criticalThreshold, 10, 200, 40);
        toolbar.Controls.Add(_criticalThreshold);

        var analyze = CreateAccentButton("ANALYZE GAME");
        analyze.Size = new Size(160, 34);
        analyze.Margin = new Padding(12, 0, 0, 0);
        analyze.Click += (_, _) => RunAnalysis();
        toolbar.Controls.Add(analyze);

        var readOnly = new Label
        {
            AutoSize = false,
            Width = 360,
            Height = 34,
            Margin = new Padding(18, 0, 0, 0),
            Text = "READ-ONLY AUDIT - no game data is changed",
            ForeColor = System.Drawing.Color.FromArgb(160, 210, 160),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9, FontStyle.Bold),
        };
        toolbar.Controls.Add(readOnly);
        topBars.Controls.Add(toolbar, 0, 0);

        var simulationBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10, 8, 10, 7),
            BackColor = System.Drawing.Color.FromArgb(32, 28, 29),
        };

        simulationBar.Controls.Add(CreateToolbarLabel("Class:"));

        _simulationClass.Width = 180;
        _simulationClass.DropDownStyle = ComboBoxStyle.DropDownList;
        _simulationClass.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _simulationClass.ForeColor = System.Drawing.Color.White;
        simulationBar.Controls.Add(_simulationClass);

        _matchNpcLevel.Text = "Match NPC level";
        _matchNpcLevel.Checked = true;
        _matchNpcLevel.AutoSize = true;
        _matchNpcLevel.ForeColor = System.Drawing.Color.Gainsboro;
        _matchNpcLevel.Margin = new Padding(14, 7, 8, 0);
        _matchNpcLevel.CheckedChanged += (_, _) =>
        {
            _simulationLevel.Enabled = !_matchNpcLevel.Checked;
        };
        simulationBar.Controls.Add(_matchNpcLevel);

        simulationBar.Controls.Add(CreateToolbarLabel("Level:"));
        ConfigureSimulationNumber(
            _simulationLevel,
            1,
            Math.Max(1, Options.Instance.Player.MaxLevel),
            Math.Min(10, Math.Max(1, Options.Instance.Player.MaxLevel)),
            64
        );
        _simulationLevel.Enabled = false;
        simulationBar.Controls.Add(_simulationLevel);

        simulationBar.Controls.Add(CreateToolbarLabel("Party:"));
        ConfigureSimulationNumber(_partySize, 1, 5, 1, 52);
        simulationBar.Controls.Add(_partySize);

        simulationBar.Controls.Add(CreateToolbarLabel("Target TTK:"));
        ConfigureSimulationNumber(_targetTtk, 1, 120, 8, 62);
        _targetTtk.DecimalPlaces = 1;
        _targetTtk.Increment = 0.5M;
        simulationBar.Controls.Add(_targetTtk);

        simulationBar.Controls.Add(CreateToolbarLabel("HP loss %:"));
        ConfigureSimulationNumber(_targetHpLoss, 1, 100, 20, 62);
        _targetHpLoss.DecimalPlaces = 1;
        _targetHpLoss.Increment = 1M;
        simulationBar.Controls.Add(_targetHpLoss);

        var simulate = CreateAccentButton("SIMULATE");
        simulate.Size = new Size(125, 32);
        simulate.Margin = new Padding(12, 0, 0, 0);
        simulate.Click += (_, _) =>
        {
            RunCombatSimulation();
            RefreshGrid();
        };
        simulationBar.Controls.Add(simulate);

        topBars.Controls.Add(simulationBar, 0, 1);

        var optionsBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10, 7, 10, 6),
            BackColor = System.Drawing.Color.FromArgb(27, 24, 25),
        };

        optionsBar.Controls.Add(CreateToolbarLabel("Expected gear:"));

        _gearProfile.Width = 185;
        _gearProfile.DropDownStyle = ComboBoxStyle.DropDownList;
        _gearProfile.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        _gearProfile.ForeColor = System.Drawing.Color.White;
        _gearProfile.Items.AddRange(
            new object[]
            {
                "No gear",
                "Class starting gear",
                "Median per slot",
                "Upper quartile per slot",
                "Best per slot",
            }
        );
        _gearProfile.SelectedIndex = 1;
        optionsBar.Controls.Add(_gearProfile);

        _includeClassSpells.Text = "Include learned class spells";
        _includeClassSpells.Checked = true;
        _includeClassSpells.AutoSize = true;
        _includeClassSpells.ForeColor = System.Drawing.Color.Gainsboro;
        _includeClassSpells.Margin = new Padding(16, 7, 8, 0);
        optionsBar.Controls.Add(_includeClassSpells);

        var simInfo = new Label
        {
            AutoSize = false,
            Width = 620,
            Height = 32,
            Margin = new Padding(18, 0, 0, 0),
            Text = "Gear uses real equipment by slot; spells include cooldown, cast time, DoT and mana sustain. Dynamic item requirements are not auto-resolved yet.",
            ForeColor = System.Drawing.Color.Silver,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        optionsBar.Controls.Add(simInfo);

        topBars.Controls.Add(optionsBar, 0, 2);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(10),
            BackColor = System.Drawing.Color.FromArgb(18, 18, 18),
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        root.Controls.Add(body, 0, 1);

        var scopePanel = CreateSection("SCOPE");
        _scope.Dock = DockStyle.Fill;
        _scope.BackColor = System.Drawing.Color.FromArgb(38, 32, 34);
        _scope.ForeColor = System.Drawing.Color.Gainsboro;
        _scope.BorderStyle = BorderStyle.None;
        _scope.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10);
        _scope.Items.AddRange(new object[]
        {
            "Overview",
            "NPCs",
            "Combat Simulation",
            "Equipment / Items",
            "Spells",
            "Resources",
            "Classes",
        });
        _scope.SelectedIndex = 0;
        _scope.SelectedIndexChanged += (_, _) => RefreshGrid();
        scopePanel.Controls.Add(_scope, 0, 1);
        body.Controls.Add(scopePanel, 0, 0);

        var resultsPanel = CreateSection("BALANCE AUDIT");
        ConfigureGrid();
        resultsPanel.Controls.Add(_grid, 0, 1);
        body.Controls.Add(resultsPanel, 1, 0);

        var detailPanel = CreateSection("DETAILS / SUGGESTION");
        _details.Dock = DockStyle.Fill;
        _details.Multiline = true;
        _details.ReadOnly = true;
        _details.ScrollBars = ScrollBars.Vertical;
        _details.BackColor = System.Drawing.Color.FromArgb(32, 28, 29);
        _details.ForeColor = System.Drawing.Color.Gainsboro;
        _details.BorderStyle = BorderStyle.None;
        _details.Font = new Font("Consolas", 10);
        detailPanel.Controls.Add(_details, 0, 1);
        body.Controls.Add(detailPanel, 2, 0);

        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
        };
        root.Controls.Add(footer, 0, 2);

        _summary.AutoSize = false;
        _summary.Location = new Point(14, 7);
        _summary.Size = new Size(1050, 32);
        _summary.ForeColor = System.Drawing.Color.Silver;
        _summary.TextAlign = ContentAlignment.MiddleLeft;
        footer.Controls.Add(_summary);

        var progressionButton = CreateAccentButton("LEVEL 1 -> MAX GRAPH");
        progressionButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        progressionButton.Size = new Size(210, 32);
        progressionButton.Click += (_, _) => ShowSelectedProgression();
        footer.Controls.Add(progressionButton);

        var openButton = CreateAccentButton("OPEN SELECTED IN EDITOR");
        openButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        openButton.Size = new Size(270, 32);
        openButton.Click += (_, _) => OpenSelected();
        footer.Controls.Add(openButton);

        footer.Resize += (_, _) =>
        {
            openButton.Left = Math.Max(10, footer.ClientSize.Width - openButton.Width - 14);
            progressionButton.Left = Math.Max(
                10,
                openButton.Left - progressionButton.Width - 10
            );
            progressionButton.Top = 7;
            openButton.Top = 7;
        };
    }

    private static Label CreateToolbarLabel(string text)
    {
        return new Label
        {
            AutoSize = false,
            Width = text.Length * 8 + 16,
            Height = 32,
            Text = text,
            ForeColor = System.Drawing.Color.Gainsboro,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(8, 0, 2, 0),
        };
    }

    private static void ConfigureThreshold(NumericUpDown number, int min, int max, int value)
    {
        number.Minimum = min;
        number.Maximum = max;
        number.Value = value;
        number.Width = 64;
        number.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        number.ForeColor = System.Drawing.Color.White;
        number.BorderStyle = BorderStyle.FixedSingle;
        number.TextAlign = HorizontalAlignment.Center;
    }

    private static void ConfigureSimulationNumber(
        NumericUpDown number,
        decimal min,
        decimal max,
        decimal value,
        int width
    )
    {
        number.Minimum = min;
        number.Maximum = max;
        number.Value = Math.Clamp(value, min, max);
        number.Width = width;
        number.BackColor = System.Drawing.Color.FromArgb(45, 38, 40);
        number.ForeColor = System.Drawing.Color.White;
        number.BorderStyle = BorderStyle.FixedSingle;
        number.TextAlign = HorizontalAlignment.Center;
    }

    private static Button CreateAccentButton(string text)
    {
        return new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(247, 69, 96),
            ForeColor = System.Drawing.Color.White,
            FlatAppearance = { BorderColor = System.Drawing.Color.FromArgb(247, 69, 96) },
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
    }

    private static TableLayoutPanel CreateSection(string title)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5),
            Padding = new Padding(8),
            ColumnCount = 1,
            RowCount = 2,
            BackColor = System.Drawing.Color.FromArgb(25, 22, 23),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(
            new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.FromArgb(247, 69, 96),
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
            },
            0,
            0
        );

        return panel;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.AutoGenerateColumns = false;
        _grid.BackgroundColor = System.Drawing.Color.FromArgb(32, 28, 29);
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = System.Drawing.Color.FromArgb(60, 52, 54);
        _grid.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(38, 32, 34);
        _grid.DefaultCellStyle.ForeColor = System.Drawing.Color.Gainsboro;
        _grid.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(72, 54, 58);
        _grid.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.White;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(55, 47, 49);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.White;
        _grid.EnableHeadersVisualStyles = false;

        AddGridColumn("Type", 90);
        AddGridColumn("Name", 220);
        AddGridColumn("Group", 110);
        AddGridColumn("Power", 90);
        AddGridColumn("Baseline", 90);
        AddGridColumn("Deviation", 90);
        AddGridColumn("Status", 85);
        AddGridColumn("TTK", 75);
        AddGridColumn("HP Lost", 80);
        AddGridColumn("Sim", 95);

        _grid.SelectionChanged += (_, _) => ShowSelectedDetails();
        _grid.CellDoubleClick += (_, _) => OpenSelected();
    }

    private void AddGridColumn(string name, int width)
    {
        _grid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = name,
                Width = width,
                SortMode = DataGridViewColumnSortMode.Automatic,
            }
        );
    }

    private void ApplyProfile()
    {
        switch (_profile.SelectedIndex)
        {
            case 0:
                _warningThreshold.Value = 30;
                _criticalThreshold.Value = 60;
                break;
            case 2:
                _warningThreshold.Value = 12;
                _criticalThreshold.Value = 25;
                break;
            default:
                _warningThreshold.Value = 20;
                _criticalThreshold.Value = 40;
                break;
        }
    }

    private void LoadSimulationClasses()
    {
        _simulationClasses.Clear();
        _simulationClass.Items.Clear();

        var all = new ClassChoice
        {
            Id = null,
            Name = "Average all classes",
        };
        _simulationClasses.Add(all);
        _simulationClass.Items.Add(all);

        foreach (var pair in ClassDescriptor.Lookup
                     .Where(pair => pair.Value != null)
                     .OrderBy(pair => Text(pair.Value!, "Name", "Unnamed Class"), StringComparer.OrdinalIgnoreCase))
        {
            var choice = new ClassChoice
            {
                Id = pair.Key,
                Name = Text(pair.Value!, "Name", "Unnamed Class"),
            };
            _simulationClasses.Add(choice);
            _simulationClass.Items.Add(choice);
        }

        _simulationClass.SelectedIndex = 0;
    }

    private void RunCombatSimulation()
    {
        foreach (var entry in _entries.Where(entry => entry.Kind == BalanceObjectKind.Npc))
        {
            entry.SimulationTtk = null;
            entry.SimulationHpLoss = null;
            entry.SimulationPlayerDps = null;
            entry.SimulationNpcDps = null;
            entry.SimulationSuggestedHp = null;
            entry.SimulationSuggestedDamage = null;
            entry.SimulationLevel = null;
            entry.SimulationClass = string.Empty;
            entry.SimulationStatus = string.Empty;
            entry.SimulationNotes = string.Empty;
        }

        var selectedChoice = _simulationClass.SelectedItem as ClassChoice;
        var classes = new List<object>();

        if (selectedChoice?.Id is Guid selectedId &&
            ClassDescriptor.Lookup.TryGetValue(selectedId, out var selectedClass) &&
            selectedClass != null)
        {
            classes.Add(selectedClass);
        }
        else
        {
            classes.AddRange(
                ClassDescriptor.Lookup.Values
                    .Where(value => value != null)
                    .Cast<object>()
            );
        }

        if (classes.Count == 0)
        {
            return;
        }

        var partySize = Math.Clamp((int)_partySize.Value, 1, 5);
        var targetTtk = Math.Max(0.1, (double)_targetTtk.Value);
        var targetHpLoss = Math.Max(0.1, (double)_targetHpLoss.Value);
        var gearProfile = Math.Max(0, _gearProfile.SelectedIndex);
        var includeClassSpells = _includeClassSpells.Checked;

        foreach (var entry in _entries.Where(entry => entry.Kind == BalanceObjectKind.Npc))
        {
            if (!NPCDescriptor.Lookup.TryGetValue(entry.Id, out var npc) || npc == null)
            {
                continue;
            }

            var npcLevel = Math.Max(1, (int)Math.Round(Number(npc, "Level")));
            var level = _matchNpcLevel.Checked
                ? npcLevel
                : Math.Clamp(
                    (int)_simulationLevel.Value,
                    1,
                    Math.Max(1, Options.Instance.Player.MaxLevel)
                );

            var results = classes
                .Select(
                    playerClass => SimulateClassVsNpc(
                        playerClass,
                        npc,
                        level,
                        partySize,
                        gearProfile,
                        includeClassSpells,
                        targetTtk
                    )
                )
                .Where(result => result != null)
                .Cast<CombatSimulation>()
                .ToArray();

            if (results.Length == 0)
            {
                continue;
            }

            var ttk = results.Average(result => result.TtkSeconds);
            var hpLoss = results.Average(result => result.HpLossPercent);
            var outgoingDps = results.Average(result => result.PlayerDps) * partySize;
            var autoAttackDps = results.Average(result => result.AutoAttackDps) * partySize;
            var spellDps = results.Average(result => result.SpellDps) * partySize;
            var manaUse = results.Average(result => result.ManaUsePerSecond) * partySize;
            var gearPower = results.Average(result => result.GearPower);
            var incomingDps = results.Average(result => result.NpcDps);
            var worstHpLoss = results.Max(result => result.HpLossPercent);
            var bestHpLoss = results.Min(result => result.HpLossPercent);
            var minTtk = results.Min(result => result.TtkSeconds);
            var maxTtk = results.Max(result => result.TtkSeconds);

            var npcHp = Math.Max(1d, Indexed(npc, "MaxVitals", 0));
            var npcBaseDamage = Math.Max(0d, Number(npc, "Damage"));

            entry.SimulationTtk = ttk;
            entry.SimulationHpLoss = hpLoss;
            entry.SimulationPlayerDps = outgoingDps;
            entry.SimulationNpcDps = incomingDps;
            entry.SimulationLevel = level;
            entry.SimulationClass = selectedChoice?.Id == null
                ? $"Average of {results.Length} class(es)"
                : selectedChoice.Name;

            entry.SimulationSuggestedHp = ttk > 0
                ? Math.Max(1d, npcHp * targetTtk / ttk)
                : npcHp;

            entry.SimulationSuggestedDamage =
                hpLoss > 0.01 && npcBaseDamage > 0
                    ? Math.Max(0d, npcBaseDamage * targetHpLoss / hpLoss)
                    : npcBaseDamage;

            var tooHard =
                hpLoss >= 100d ||
                ttk > targetTtk * 1.35 ||
                hpLoss > targetHpLoss * 1.50;

            var tooEasy =
                ttk < targetTtk * 0.65 &&
                hpLoss < targetHpLoss * 0.55;

            entry.SimulationStatus = tooHard
                ? "TOO HARD"
                : tooEasy
                    ? "TOO EASY"
                    : "TARGET";

            var loadoutText = results.Length == 1
                ? results[0].GearSummary
                : $"{_gearProfile.Text}; actual selected items vary by class only when weapon behavior differs.";

            var spellText = results.Length == 1
                ? results[0].SpellSummary
                : includeClassSpells
                    ? "Learned spell rotations are calculated independently for each class."
                    : "Class spells disabled.";

            entry.SimulationNotes =
                $"Simulation basis: {entry.SimulationClass}\r\n" +
                $"Player level: {level}\r\n" +
                $"Party size: {partySize}\r\n" +
                $"Expected gear: {_gearProfile.Text} (avg gear power {gearPower:0.0})\r\n" +
                $"Class spells: {(includeClassSpells ? "ON" : "OFF")}\r\n" +
                $"Outgoing party DPS: {outgoingDps:0.0}\r\n" +
                $"  Auto-attack DPS: {autoAttackDps:0.0}\r\n" +
                $"  Spell DPS: {spellDps:0.0}\r\n" +
                $"  Mana use: {manaUse:0.0}/sec\r\n" +
                $"Incoming NPC DPS: {incomingDps:0.0}\r\n" +
                $"Estimated TTK: {ttk:0.00}s (target {targetTtk:0.0}s)\r\n" +
                $"Estimated party-average HP lost: {hpLoss:0.0}% (target {targetHpLoss:0.0}%)\r\n" +
                (results.Length > 1
                    ? $"Class range TTK: {minTtk:0.00}s - {maxTtk:0.00}s\r\n" +
                      $"Class range HP lost: {bestHpLoss:0.0}% - {worstHpLoss:0.0}%\r\n"
                    : string.Empty) +
                $"Suggested NPC HP toward TTK target: {entry.SimulationSuggestedHp:0}\r\n" +
                $"Suggested base damage toward HP-loss target: {entry.SimulationSuggestedDamage:0.##}\r\n\r\n" +
                $"GEAR\r\n----\r\n{loadoutText}\r\n\r\n" +
                $"SPELL ROTATION\r\n--------------\r\n{spellText}\r\n\r\n" +
                "Combat math follows Intersect's default physical/magic/true damage formulas, class growth, " +
                "item stat stacking, attack speed, cast/cooldown timing and mana sustain. Dynamic item usage " +
                "requirements, movement, blocking, status-control value and custom formulas still require designer review.";
        }
    }

    private static CombatSimulation? SimulateClassVsNpc(
        object playerClass,
        object npc,
        int level,
        int partySize,
        int gearProfile,
        bool includeClassSpells,
        double sustainWindowSeconds
    )
    {
        var stats = new double[5];
        var increasePercentage = Convert.ToBoolean(Property(playerClass, "IncreasePercentage") ?? false);

        for (var i = 0; i < stats.Length; i++)
        {
            var baseStat = Math.Max(0d, Indexed(playerClass, "BaseStat", i));
            var increase = Indexed(playerClass, "StatIncrease", i);
            stats[i] = ScaleByLevel(baseStat, increase, increasePercentage, level);
        }

        var baseHp = Math.Max(
            1d,
            ScaleByLevel(
                Math.Max(1d, Indexed(playerClass, "BaseVital", 0)),
                Indexed(playerClass, "VitalIncrease", 0),
                increasePercentage,
                level
            )
        );

        var baseMana = Math.Max(
            0d,
            ScaleByLevel(
                Math.Max(0d, Indexed(playerClass, "BaseVital", 1)),
                Indexed(playerClass, "VitalIncrease", 1),
                increasePercentage,
                level
            )
        );

        var loadout = BuildLoadout(playerClass, gearProfile);

        for (var i = 0; i < stats.Length; i++)
        {
            var flat = stats[i] + loadout.FlatStats[i];
            stats[i] = Math.Max(1d, Math.Ceiling(flat + flat * loadout.PercentStats[i] / 100d));
        }

        var playerHp = Math.Max(
            1d,
            baseHp +
            loadout.FlatVitals[0] +
            baseHp * loadout.PercentVitals[0] / 100d
        );

        var playerMana = Math.Max(
            0d,
            baseMana +
            loadout.FlatVitals[1] +
            baseMana * loadout.PercentVitals[1] / 100d
        );

        var npcHp = Math.Max(1d, Indexed(npc, "MaxVitals", 0));
        var npcStats = new double[5];
        for (var i = 0; i < npcStats.Length; i++)
        {
            npcStats[i] = Math.Max(0d, Indexed(npc, "Stats", i));
        }

        var combatSource = loadout.Weapon ?? playerClass;
        var playerBaseDamage = Math.Max(0d, Number(combatSource, "Damage"));
        var playerDamageType = (DamageType)(int)Number(combatSource, "DamageType");
        var playerScalingStat = Math.Clamp((int)Number(combatSource, "ScalingStat"), 0, stats.Length - 1);
        var playerScaling = (int)Number(combatSource, "Scaling");
        var playerCritChance = Math.Clamp(Number(combatSource, "CritChance"), 0d, 100d);
        var playerCritMultiplier = Math.Max(1d, Number(combatSource, "CritMultiplier"));

        var playerHit = AverageDamage(
            playerBaseDamage,
            playerDamageType,
            stats[playerScalingStat],
            playerScaling,
            playerCritChance,
            playerCritMultiplier,
            npcStats[(int)Stat.Defense],
            npcStats[(int)Stat.MagicResist]
        );

        var playerAttackMs = CalculatePlayerAttackTimeMs(
            stats[(int)Stat.Speed],
            playerClass,
            loadout.Weapon
        );

        var playerAttackSeconds = Math.Max(0.05, playerAttackMs / 1000d);
        var rawAutoAttackDps = playerHit / playerAttackSeconds;

        var manaRegenPerSecond = CalculateManaRegenPerSecond(
            playerClass,
            playerMana,
            loadout.VitalRegen[1]
        );

        var spellRotation = EstimateSpellRotation(
            playerClass,
            level,
            stats,
            npcStats,
            playerMana,
            manaRegenPerSecond,
            includeClassSpells,
            sustainWindowSeconds
        );

        var autoAttackDps =
            rawAutoAttackDps * Math.Max(0.15d, 1d - spellRotation.CastOccupancy);

        var perPlayerDps = Math.Max(0.0001, autoAttackDps + spellRotation.Dps);
        var partyDps = Math.Max(0.0001, perPlayerDps * Math.Max(1, partySize));

        var npcBaseDamage = Math.Max(0d, Number(npc, "Damage"));
        var npcDamageType = (DamageType)(int)Number(npc, "DamageType");
        var npcScalingStat = Math.Clamp((int)Number(npc, "ScalingStat"), 0, npcStats.Length - 1);
        var npcScaling = (int)Number(npc, "Scaling");
        var npcCritChance = Math.Clamp(Number(npc, "CritChance"), 0d, 100d);
        var npcCritMultiplier = Math.Max(1d, Number(npc, "CritMultiplier"));

        var npcHit = AverageDamage(
            npcBaseDamage,
            npcDamageType,
            npcStats[npcScalingStat],
            npcScaling,
            npcCritChance,
            npcCritMultiplier,
            stats[(int)Stat.Defense],
            stats[(int)Stat.MagicResist]
        );

        var npcAttackMs = CalculateAttackTimeMs(
            npcStats[(int)Stat.Speed],
            (int)Number(npc, "AttackSpeedModifier"),
            (int)Number(npc, "AttackSpeedValue"),
            subtractPingAllowance: false
        );

        var npcAttackSeconds = Math.Max(0.05, npcAttackMs / 1000d);
        var npcDps = npcHit / npcAttackSeconds;

        var ttk = npcHp / partyDps;
        var pooledPartyHp = playerHp * Math.Max(1, partySize);
        var hpLoss = pooledPartyHp <= 0
            ? 100d
            : npcDps * ttk / pooledPartyHp * 100d;

        var timeToWipe = npcDps <= 0.0001
            ? double.PositiveInfinity
            : pooledPartyHp / npcDps;

        return new CombatSimulation
        {
            Level = level,
            ClassName = Text(playerClass, "Name", "Unnamed Class"),
            PlayerHp = playerHp,
            PlayerMana = playerMana,
            PlayerDamagePerHit = playerHit,
            PlayerAttackSeconds = playerAttackSeconds,
            PlayerDps = perPlayerDps,
            AutoAttackDps = autoAttackDps,
            SpellDps = spellRotation.Dps,
            ManaUsePerSecond = spellRotation.ManaPerSecond,
            GearPower = loadout.Power,
            GearSummary = loadout.Summary,
            SpellSummary = spellRotation.Summary,
            NpcDamagePerHit = npcHit,
            NpcAttackSeconds = npcAttackSeconds,
            NpcDps = npcDps,
            TtkSeconds = ttk,
            HpLossPercent = hpLoss,
            TimeToPartyWipeSeconds = timeToWipe,
        };
    }

    private static LoadoutSnapshot BuildLoadout(object playerClass, int profile)
    {
        var loadout = new LoadoutSnapshot();
        if (profile <= 0)
        {
            return loadout;
        }

        var selected = new Dictionary<int, object>();

        if (profile == 1)
        {
            if (Property(playerClass, "Items") is IEnumerable classItems)
            {
                foreach (var classItem in classItems)
                {
                    if (classItem == null)
                    {
                        continue;
                    }

                    var itemId = GuidValue(classItem, "Id");
                    if (itemId == Guid.Empty ||
                        !ItemDescriptor.Lookup.TryGetValue(itemId, out var item) ||
                        item == null ||
                        (int)Number(item, "ItemType") != (int)ItemType.Equipment)
                    {
                        continue;
                    }

                    var slot = (int)Number(item, "EquipmentSlot");
                    if (slot < 0 || slot >= Options.Instance.Equipment.Slots.Count)
                    {
                        continue;
                    }

                    if (!selected.TryGetValue(slot, out var current) ||
                        CalculateEquipmentPower(item) > CalculateEquipmentPower(current))
                    {
                        selected[slot] = item;
                    }
                }
            }
        }
        else
        {
            var bySlot = ItemDescriptor.Lookup.Values
                .Where(item =>
                    item != null &&
                    (int)Number(item, "ItemType") == (int)ItemType.Equipment)
                .Where(item =>
                {
                    var slot = (int)Number(item!, "EquipmentSlot");
                    return slot >= 0 && slot < Options.Instance.Equipment.Slots.Count;
                })
                .GroupBy(item => (int)Number(item!, "EquipmentSlot"))
                .ToDictionary(group => group.Key, group => group.Cast<object>().ToArray());

            var percentile = profile switch
            {
                2 => 0.50d,
                3 => 0.75d,
                _ => 1.00d,
            };

            foreach (var pair in bySlot)
            {
                var ordered = pair.Value
                    .OrderBy(CalculateEquipmentPower)
                    .ThenBy(item => Text(item, "Name", string.Empty), StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (ordered.Length == 0)
                {
                    continue;
                }

                var index = (int)Math.Round(
                    (ordered.Length - 1) * percentile,
                    MidpointRounding.AwayFromZero
                );
                index = Math.Clamp(index, 0, ordered.Length - 1);
                selected[pair.Key] = ordered[index];
            }
        }

        if (selected.TryGetValue(Options.Instance.Equipment.WeaponSlot, out var selectedWeapon) &&
            Convert.ToBoolean(Property(selectedWeapon, "TwoHanded") ?? false))
        {
            selected.Remove(Options.Instance.Equipment.ShieldSlot);
        }

        var summary = new List<string>();
        foreach (var pair in selected.OrderBy(pair => pair.Key))
        {
            var item = pair.Value;
            for (var i = 0; i < loadout.FlatStats.Length; i++)
            {
                loadout.FlatStats[i] += Indexed(item, "StatsGiven", i);
                loadout.PercentStats[i] += Indexed(item, "PercentageStatsGiven", i);
            }

            for (var i = 0; i < loadout.FlatVitals.Length; i++)
            {
                loadout.FlatVitals[i] += Indexed(item, "VitalsGiven", i);
                loadout.PercentVitals[i] += Indexed(item, "PercentageVitalsGiven", i);
                loadout.VitalRegen[i] += Indexed(item, "VitalsRegen", i);
            }

            var power = CalculateEquipmentPower(item);
            loadout.Power += power;
            loadout.ItemCount++;

            var slotName = pair.Key >= 0 && pair.Key < Options.Instance.Equipment.Slots.Count
                ? Options.Instance.Equipment.Slots[pair.Key]
                : $"Slot {pair.Key}";

            summary.Add($"{slotName}: {Text(item, "Name", "Unnamed Item")} (power {power:0.0})");

            if (pair.Key == Options.Instance.Equipment.WeaponSlot)
            {
                loadout.Weapon = item;
            }
        }

        loadout.Summary = summary.Count == 0
            ? "No matching equipment found."
            : string.Join("\r\n", summary);

        return loadout;
    }

    private static double CalculateEquipmentPower(object item)
    {
        var statTotal = 0d;
        var percentageStats = 0d;

        for (var i = 0; i < 5; i++)
        {
            statTotal += Math.Abs(Indexed(item, "StatsGiven", i));
            percentageStats += Math.Abs(Indexed(item, "PercentageStatsGiven", i));
        }

        var hp = Math.Abs(Indexed(item, "VitalsGiven", 0));
        var mp = Math.Abs(Indexed(item, "VitalsGiven", 1));
        var hpPercent = Math.Abs(Indexed(item, "PercentageVitalsGiven", 0));
        var mpPercent = Math.Abs(Indexed(item, "PercentageVitalsGiven", 1));
        var damage = Math.Abs(Number(item, "Damage"));
        var crit = Math.Abs(Number(item, "CritChance"));
        var block = Math.Abs(Number(item, "BlockChance"));
        var scaling = Math.Abs(Number(item, "Scaling"));

        return Math.Max(
            0.01,
            statTotal +
            percentageStats * 2.0 +
            hp * 0.08 +
            mp * 0.035 +
            hpPercent * 1.8 +
            mpPercent * 0.8 +
            damage * 1.8 +
            crit * 1.2 +
            block * 1.0 +
            scaling * 0.8
        );
    }

    private static SpellRotationSnapshot EstimateSpellRotation(
        object playerClass,
        int level,
        double[] stats,
        double[] npcStats,
        double playerMana,
        double manaRegenPerSecond,
        bool includeSpells,
        double sustainWindowSeconds
    )
    {
        var result = new SpellRotationSnapshot();
        if (!includeSpells)
        {
            return result;
        }

        if (Property(playerClass, "Spells") is not IEnumerable classSpells)
        {
            result.Summary = "No class spell list found.";
            return result;
        }

        var spellRows = new List<(string Name, double Dps, double ManaPerSecond, double Occupancy)>();

        foreach (var classSpell in classSpells)
        {
            if (classSpell == null || Number(classSpell, "Level") > level)
            {
                continue;
            }

            var spellId = GuidValue(classSpell, "Id");
            if (spellId == Guid.Empty ||
                !SpellDescriptor.Lookup.TryGetValue(spellId, out var spell) ||
                spell == null ||
                (SpellType)(int)Number(spell, "SpellType") != SpellType.CombatSpell)
            {
                continue;
            }

            var combat = Property(spell, "Combat");
            if (combat == null ||
                Convert.ToBoolean(Property(combat, "Friendly") ?? false))
            {
                continue;
            }

            var baseDamage = Math.Max(0d, Indexed(combat, "VitalDiff", 0));
            if (baseDamage <= 0)
            {
                continue;
            }

            var scalingStat = Math.Clamp((int)Number(combat, "ScalingStat"), 0, stats.Length - 1);
            var hitDamage = AverageDamage(
                baseDamage,
                (DamageType)(int)Number(combat, "DamageType"),
                stats[scalingStat],
                (int)Number(combat, "Scaling"),
                Math.Clamp(Number(combat, "CritChance"), 0d, 100d),
                Math.Max(1d, Number(combat, "CritMultiplier")),
                npcStats[(int)Stat.Defense],
                npcStats[(int)Stat.MagicResist]
            );

            var totalDamage = hitDamage;
            if (Convert.ToBoolean(Property(combat, "HoTDoT") ?? false))
            {
                var duration = Math.Max(0d, Number(combat, "Duration"));
                var interval = Math.Max(0d, Number(combat, "HotDotInterval"));
                if (duration > 0 && interval > 0)
                {
                    var extraTicks = Math.Ceiling(duration / interval);
                    totalDamage += hitDamage * extraTicks;
                }
            }

            var castMs = Math.Max(0d, Number(spell, "CastDuration"));
            var cooldownMs = Math.Max(0d, Number(spell, "CooldownDuration"));
            var ignoresGlobal = Convert.ToBoolean(Property(spell, "IgnoreGlobalCooldown") ?? false);
            var globalMs =
                Options.Instance.Combat.EnableGlobalCooldowns && !ignoresGlobal
                    ? Math.Max(0d, Options.Instance.Combat.GlobalCooldownDuration)
                    : 0d;

            var cycleMs = Math.Max(250d, castMs + Math.Max(cooldownMs, globalMs));
            var castsPerSecond = 1000d / cycleMs;
            var dps = totalDamage * castsPerSecond;
            var manaCost = Math.Max(0d, Indexed(spell, "VitalCost", 1));
            var manaPerSecond = manaCost * castsPerSecond;
            var occupancy = Math.Clamp(castMs / cycleMs, 0d, 1d);

            spellRows.Add(
                (
                    Text(spell, "Name", "Unnamed Spell"),
                    dps,
                    manaPerSecond,
                    occupancy
                )
            );
        }

        if (spellRows.Count == 0)
        {
            result.Summary = "No damaging class spells are learned at this level.";
            return result;
        }

        var rawDps = spellRows.Sum(row => row.Dps);
        var rawManaPerSecond = spellRows.Sum(row => row.ManaPerSecond);
        var rawOccupancy = spellRows.Sum(row => row.Occupancy);

        var occupancyScale = rawOccupancy > 0.85d
            ? 0.85d / rawOccupancy
            : 1d;

        rawDps *= occupancyScale;
        rawManaPerSecond *= occupancyScale;
        rawOccupancy *= occupancyScale;

        var window = Math.Max(1d, sustainWindowSeconds);
        var sustainableManaPerSecond =
            playerMana / window + Math.Max(0d, manaRegenPerSecond);

        var manaScale =
            rawManaPerSecond > 0.0001 && rawManaPerSecond > sustainableManaPerSecond
                ? Math.Clamp(sustainableManaPerSecond / rawManaPerSecond, 0d, 1d)
                : 1d;

        result.Dps = rawDps * manaScale;
        result.ManaPerSecond = rawManaPerSecond * manaScale;
        result.CastOccupancy = Math.Clamp(rawOccupancy * manaScale, 0d, 0.85d);
        result.SpellCount = spellRows.Count;

        var top = spellRows
            .OrderByDescending(row => row.Dps)
            .Take(5)
            .Select(row => $"{row.Name}: {row.Dps * occupancyScale * manaScale:0.0} DPS")
            .ToArray();

        result.Summary =
            $"Learned damaging spells: {spellRows.Count}\r\n" +
            $"Spell DPS: {result.Dps:0.0}\r\n" +
            $"Mana use: {result.ManaPerSecond:0.0}/sec\r\n" +
            $"Mana sustain budget: {sustainableManaPerSecond:0.0}/sec over {window:0.0}s\r\n" +
            $"Casting occupancy: {result.CastOccupancy * 100d:0.0}%\r\n" +
            (manaScale < 0.999
                ? $"Mana-limited rotation scale: {manaScale * 100d:0.0}%\r\n"
                : string.Empty) +
            string.Join("\r\n", top);

        return result;
    }

    private static double CalculateManaRegenPerSecond(
        object playerClass,
        double playerMana,
        double equipmentManaRegen
    )
    {
        if (!Options.Instance.Combat.RegenVitalsInCombat)
        {
            return 0d;
        }

        var regenRate = Indexed(playerClass, "VitalRegen", 1) + equipmentManaRegen;
        if (Math.Abs(regenRate) < 0.0001)
        {
            return 0d;
        }

        var regenIntervalSeconds = Math.Max(0.1d, Options.Instance.Combat.RegenTime / 1000d);
        var regenPerTick =
            Math.Max(1d, playerMana * Math.Abs(regenRate) / 100d) *
            Math.Sign(regenRate);

        return regenPerTick / regenIntervalSeconds;
    }

    private static double CalculatePlayerAttackTimeMs(
        double speed,
        object playerClass,
        object? weapon
    )
    {
        var attackTime = CalculateBaseAttackTimeMs(speed);

        if ((int)Number(playerClass, "AttackSpeedModifier") == 1 &&
            Number(playerClass, "AttackSpeedValue") > 0)
        {
            attackTime = Number(playerClass, "AttackSpeedValue");
        }

        if (weapon != null)
        {
            var weaponModifier = (int)Number(weapon, "AttackSpeedModifier");
            var weaponValue = Number(weapon, "AttackSpeedValue");

            if (weaponModifier == 1 && weaponValue > 0)
            {
                attackTime = weaponValue;
            }
            else if (weaponModifier == 2 && weaponValue > 0)
            {
                attackTime *= 100d / weaponValue;
            }
        }

        return Math.Max(50d, attackTime - 60d);
    }

    private static double CalculateBaseAttackTimeMs(double speed)
    {
        var maxStat = Math.Max(1d, Options.Instance.Player.MaxStat);
        var clampedSpeed = Math.Clamp(speed, 0d, maxStat);

        return
            Options.Instance.Combat.MaxAttackRate +
            (Options.Instance.Combat.MinAttackRate - Options.Instance.Combat.MaxAttackRate) *
            ((maxStat - clampedSpeed) / maxStat);
    }

    private static double ScaleByLevel(
        double baseValue,
        double increase,
        bool percentageIncrease,
        int level
    )
    {
        var safeLevel = Math.Max(1, level);
        if (percentageIncrease)
        {
            return baseValue * Math.Pow(1d + increase / 100d, safeLevel - 1);
        }

        return baseValue + increase * (safeLevel - 1);
    }

    private static double AverageDamage(
        double baseDamage,
        DamageType damageType,
        double scalingStat,
        int scaling,
        double critChance,
        double critMultiplier,
        double victimDefense,
        double victimMagicResist
    )
    {
        var scaled = baseDamage + scalingStat * scaling / 100d;

        // The default Intersect formula randomizes each hit from 97.5%-102.5%.
        // Its expected value is 100%, so the Balance Lab uses the mean.
        var averageCritMultiplier =
            1d + Math.Clamp(critChance, 0d, 100d) / 100d * (Math.Max(1d, critMultiplier) - 1d);

        var raw = Math.Max(0d, scaled * averageCritMultiplier);
        return damageType switch
        {
            DamageType.Physical => raw * (100d / (100d + Math.Max(0d, victimDefense))),
            DamageType.Magic => raw * (100d / (100d + Math.Max(0d, victimMagicResist))),
            _ => raw,
        };
    }

    private static double CalculateAttackTimeMs(
        double speed,
        int attackSpeedModifier,
        int attackSpeedValue,
        bool subtractPingAllowance
    )
    {
        var attackTime = CalculateBaseAttackTimeMs(speed);

        if (attackSpeedModifier == 1 && attackSpeedValue > 0)
        {
            attackTime = attackSpeedValue;
        }

        if (subtractPingAllowance)
        {
            attackTime -= 60d;
        }

        return Math.Max(50d, attackTime);
    }

    private void RunAnalysis()
    {
        _entries.Clear();

        AnalyzeNpcs();
        AnalyzeItems();
        AnalyzeSpells();
        AnalyzeResources();
        AnalyzeClasses();

        ApplyBaselinesAndSeverity();
        RunCombatSimulation();
        RefreshGrid();
    }

    private void AnalyzeNpcs()
    {
        foreach (var pair in NPCDescriptor.Lookup)
        {
            var npc = pair.Value;
            if (npc == null)
            {
                continue;
            }

            var level = Math.Max(1d, Number(npc, "Level"));
            var hp = Math.Max(1d, Indexed(npc, "MaxVitals", 0));
            var mana = Math.Max(0d, Indexed(npc, "MaxVitals", 1));
            var attack = Math.Max(0d, Indexed(npc, "Stats", 0));
            var ap = Math.Max(0d, Indexed(npc, "Stats", 1));
            var defense = Math.Max(0d, Indexed(npc, "Stats", 2));
            var mr = Math.Max(0d, Indexed(npc, "Stats", 3));
            var speed = Math.Max(0d, Indexed(npc, "Stats", 4));
            var damage = Math.Max(0d, Number(npc, "Damage"));
            var exp = Math.Max(0d, Number(npc, "Experience"));
            var crit = Math.Max(0d, Number(npc, "CritChance"));

            var offense = damage * 2.0 + attack * 0.75 + ap * 0.65 + speed * 0.20 + crit * 0.30;
            var durability = hp * (1d + (defense + mr) / 220d);
            var power = Math.Max(1d, Math.Sqrt(durability) * Math.Max(1d, offense));

            _entries.Add(
                new BalanceEntry
                {
                    Kind = BalanceObjectKind.Npc,
                    Id = pair.Key,
                    Name = Text(npc, "Name", "Unnamed NPC"),
                    Group = $"Lv {level:0}",
                    Power = power,
                    Reward = exp,
                    Metrics =
                        $"Level: {level:0}\r\n" +
                        $"HP / MP: {hp:0} / {mana:0}\r\n" +
                        $"Attack / AP: {attack:0} / {ap:0}\r\n" +
                        $"Defense / MR: {defense:0} / {mr:0}\r\n" +
                        $"Speed: {speed:0}\r\n" +
                        $"Base damage: {damage:0}\r\n" +
                        $"Crit chance: {crit:0.##}\r\n" +
                        $"EXP reward: {exp:0}\r\n" +
                        $"Relative combat power: {power:0.0}",
                }
            );
        }
    }

    private void AnalyzeItems()
    {
        foreach (var pair in ItemDescriptor.Lookup)
        {
            var item = pair.Value;
            if (item == null)
            {
                continue;
            }

            var itemType = (int)Number(item, "ItemType");
            var slot = (int)Number(item, "EquipmentSlot");
            var rarity = (int)Number(item, "Rarity");

            var attack = Indexed(item, "StatsGiven", 0);
            var ap = Indexed(item, "StatsGiven", 1);
            var defense = Indexed(item, "StatsGiven", 2);
            var mr = Indexed(item, "StatsGiven", 3);
            var speed = Indexed(item, "StatsGiven", 4);

            var percentageStats = 0d;
            for (var i = 0; i < 5; i++)
            {
                percentageStats += Math.Abs(Indexed(item, "PercentageStatsGiven", i));
            }

            var hp = Math.Abs(Indexed(item, "VitalsGiven", 0));
            var mp = Math.Abs(Indexed(item, "VitalsGiven", 1));
            var hpPercent = Math.Abs(Indexed(item, "PercentageVitalsGiven", 0));
            var mpPercent = Math.Abs(Indexed(item, "PercentageVitalsGiven", 1));
            var damage = Math.Abs(Number(item, "Damage"));
            var crit = Math.Abs(Number(item, "CritChance"));
            var block = Math.Abs(Number(item, "BlockChance"));
            var scaling = Math.Abs(Number(item, "Scaling"));
            var price = Math.Max(0d, Number(item, "Price"));

            var statTotal =
                Math.Abs(attack) +
                Math.Abs(ap) +
                Math.Abs(defense) +
                Math.Abs(mr) +
                Math.Abs(speed);

            var power =
                statTotal +
                percentageStats * 2.0 +
                hp * 0.08 +
                mp * 0.035 +
                hpPercent * 1.8 +
                mpPercent * 0.8 +
                damage * 1.8 +
                crit * 1.2 +
                block * 1.0 +
                scaling * 0.8;

            // Keep non-equipment items visible, but prevent zero-power consumables
            // from dominating the median of actual equipment.
            power = Math.Max(0.01, power);

            var group = itemType == 1
                ? $"Equip slot {slot} / R{rarity}"
                : $"Type {itemType} / R{rarity}";

            _entries.Add(
                new BalanceEntry
                {
                    Kind = BalanceObjectKind.Item,
                    Id = pair.Key,
                    Name = Text(item, "Name", "Unnamed Item"),
                    Group = group,
                    Power = power,
                    Reward = price,
                    Metrics =
                        $"Item type: {itemType}\r\n" +
                        $"Equipment slot: {slot}\r\n" +
                        $"Rarity: {rarity}\r\n" +
                        $"Flat stats total: {statTotal:0.##}\r\n" +
                        $"Percent stats total: {percentageStats:0.##}%\r\n" +
                        $"HP / MP bonus: {hp:0.##} / {mp:0.##}\r\n" +
                        $"Damage: {damage:0.##}\r\n" +
                        $"Crit / Block: {crit:0.##} / {block:0.##}\r\n" +
                        $"Price: {price:0}\r\n" +
                        $"Estimated item power: {power:0.0}",
                }
            );
        }
    }

    private void AnalyzeSpells()
    {
        foreach (var pair in SpellDescriptor.Lookup)
        {
            var spell = pair.Value;
            if (spell == null)
            {
                continue;
            }

            var spellType = (int)Number(spell, "SpellType");
            var castMs = Math.Max(0d, Number(spell, "CastDuration"));
            var cooldownMs = Math.Max(0d, Number(spell, "CooldownDuration"));
            var hpCost = Math.Abs(Indexed(spell, "VitalCost", 0));
            var mpCost = Math.Abs(Indexed(spell, "VitalCost", 1));

            var combat = Property(spell, "Combat");
            var hpMagnitude = combat == null ? 0d : Math.Abs(Indexed(combat, "VitalDiff", 0));
            var mpMagnitude = combat == null ? 0d : Math.Abs(Indexed(combat, "VitalDiff", 1));
            var scaling = combat == null ? 0d : Math.Abs(Number(combat, "Scaling"));

            var cycleSeconds = Math.Max(0.25, (castMs + cooldownMs) / 1000d);
            var throughput = (hpMagnitude + mpMagnitude * 0.35 + scaling * 0.5) / cycleSeconds;
            var efficiencyPenalty = 1d + hpCost * 0.02 + mpCost * 0.01;
            var power = Math.Max(0.01, throughput / efficiencyPenalty);

            _entries.Add(
                new BalanceEntry
                {
                    Kind = BalanceObjectKind.Spell,
                    Id = pair.Key,
                    Name = Text(spell, "Name", "Unnamed Spell"),
                    Group = $"Type {spellType}",
                    Power = power,
                    Reward = 0,
                    Metrics =
                        $"Spell type: {spellType}\r\n" +
                        $"Cast time: {castMs:0} ms\r\n" +
                        $"Cooldown: {cooldownMs:0} ms\r\n" +
                        $"HP / MP cost: {hpCost:0.##} / {mpCost:0.##}\r\n" +
                        $"Combat HP magnitude: {hpMagnitude:0.##}\r\n" +
                        $"Combat MP magnitude: {mpMagnitude:0.##}\r\n" +
                        $"Scaling: {scaling:0.##}\r\n" +
                        $"Estimated throughput score: {power:0.0}",
                }
            );
        }
    }

    private void AnalyzeResources()
    {
        foreach (var pair in ResourceDescriptor.Lookup)
        {
            var resource = pair.Value;
            if (resource == null)
            {
                continue;
            }

            var minHp = Math.Max(0d, Number(resource, "MinHp"));
            var maxHp = Math.Max(minHp, Number(resource, "MaxHp"));
            var averageHp = (minHp + maxHp) / 2d;
            var spawn = Math.Max(0d, Number(resource, "SpawnDuration"));
            var regen = Math.Max(0d, Number(resource, "VitalRegen"));

            var drops = Property(resource, "Drops") as IEnumerable;
            var expectedDropUnits = 0d;
            if (drops != null)
            {
                foreach (var drop in drops)
                {
                    if (drop == null)
                    {
                        continue;
                    }

                    var min = Math.Max(0d, Number(drop, "MinQuantity"));
                    var max = Math.Max(min, Number(drop, "MaxQuantity"));
                    var chance = Math.Max(0d, Number(drop, "Chance"));
                    if (chance > 1d)
                    {
                        chance /= 100d;
                    }

                    expectedDropUnits += ((min + max) / 2d) * Math.Min(1d, chance);
                }
            }

            var effort =
                Math.Max(0.01, averageHp + regen * 10d + spawn / 1000d * 2d);

            _entries.Add(
                new BalanceEntry
                {
                    Kind = BalanceObjectKind.Resource,
                    Id = pair.Key,
                    Name = Text(resource, "Name", "Unnamed Resource"),
                    Group = $"Tool {(int)Number(resource, "Tool")}",
                    Power = effort,
                    Reward = expectedDropUnits,
                    Metrics =
                        $"HP range: {minHp:0} - {maxHp:0}\r\n" +
                        $"Average HP: {averageHp:0.0}\r\n" +
                        $"Respawn: {spawn:0}\r\n" +
                        $"Regen: {regen:0.##}\r\n" +
                        $"Expected drop units: {expectedDropUnits:0.##}\r\n" +
                        $"Estimated harvest effort: {effort:0.0}",
                }
            );
        }
    }

    private void AnalyzeClasses()
    {
        foreach (var pair in ClassDescriptor.Lookup)
        {
            var playerClass = pair.Value;
            if (playerClass == null)
            {
                continue;
            }

            var attack = Math.Max(0d, Indexed(playerClass, "BaseStat", 0));
            var ap = Math.Max(0d, Indexed(playerClass, "BaseStat", 1));
            var defense = Math.Max(0d, Indexed(playerClass, "BaseStat", 2));
            var mr = Math.Max(0d, Indexed(playerClass, "BaseStat", 3));
            var speed = Math.Max(0d, Indexed(playerClass, "BaseStat", 4));
            var hp = Math.Max(1d, Indexed(playerClass, "BaseVital", 0));
            var mp = Math.Max(0d, Indexed(playerClass, "BaseVital", 1));
            var damage = Math.Max(0d, Number(playerClass, "Damage"));
            var scaling = Math.Max(0d, Number(playerClass, "Scaling"));

            var statGrowth = 0d;
            for (var i = 0; i < 5; i++)
            {
                statGrowth += Math.Abs(Indexed(playerClass, "StatIncrease", i));
            }

            var vitalGrowth =
                Math.Abs(Indexed(playerClass, "VitalIncrease", 0)) * 0.08 +
                Math.Abs(Indexed(playerClass, "VitalIncrease", 1)) * 0.035;

            var baseStats = attack + ap + defense + mr + speed;
            var power =
                baseStats +
                hp * 0.08 +
                mp * 0.035 +
                damage * 2.0 +
                scaling +
                statGrowth * 4.0 +
                vitalGrowth;

            _entries.Add(
                new BalanceEntry
                {
                    Kind = BalanceObjectKind.PlayerClass,
                    Id = pair.Key,
                    Name = Text(playerClass, "Name", "Unnamed Class"),
                    Group = "Player Classes",
                    Power = Math.Max(0.01, power),
                    Reward = Math.Max(0d, Number(playerClass, "BaseExp")),
                    Metrics =
                        $"Base Attack / AP: {attack:0} / {ap:0}\r\n" +
                        $"Base Defense / MR: {defense:0} / {mr:0}\r\n" +
                        $"Base Speed: {speed:0}\r\n" +
                        $"Base HP / MP: {hp:0} / {mp:0}\r\n" +
                        $"Base damage: {damage:0.##}\r\n" +
                        $"Scaling: {scaling:0.##}\r\n" +
                        $"Stat growth total: {statGrowth:0.##}\r\n" +
                        $"Relative class power: {power:0.0}",
                }
            );
        }
    }

    private void ApplyBaselinesAndSeverity()
    {
        var warning = (double)_warningThreshold.Value;
        var critical = (double)_criticalThreshold.Value;

        foreach (var entry in _entries)
        {
            var peers = _entries
                .Where(other =>
                    other.Kind == entry.Kind &&
                    string.Equals(other.Group, entry.Group, StringComparison.OrdinalIgnoreCase))
                .Select(other => other.Power)
                .Where(value => value > 0)
                .ToArray();

            if (peers.Length < 3)
            {
                peers = _entries
                    .Where(other => other.Kind == entry.Kind)
                    .Select(other => other.Power)
                    .Where(value => value > 0)
                    .ToArray();
            }

            entry.Baseline = Median(peers);
            var deviation = Math.Abs(entry.DeviationPercent);

            entry.Severity = deviation >= critical
                ? "CRITICAL"
                : deviation >= warning
                    ? "WARNING"
                    : "OK";

            if (entry.Baseline <= 0)
            {
                entry.Suggestion = "Not enough comparable data to build a baseline.";
            }
            else if (entry.Severity == "OK")
            {
                entry.Suggestion =
                    "This entry is inside the selected tolerance compared with similar game objects.";
            }
            else
            {
                var direction = entry.DeviationPercent > 0 ? "above" : "below";
                var targetFactor = entry.Power <= 0 ? 1d : entry.Baseline / entry.Power;

                entry.Suggestion =
                    $"Relative power is {Math.Abs(entry.DeviationPercent):0.0}% {direction} its peer baseline.\r\n\r\n" +
                    $"A first-pass rebalance would move its combined power toward about " +
                    $"{targetFactor * 100d:0}% of the current value.\r\n\r\n" +
                    "Do not apply this as a blind multiplier. Open the source editor and decide " +
                    "which stats are intended to define this object (HP, damage, defense, cooldown, rewards, etc.).";
            }

            if (entry.Kind == BalanceObjectKind.Npc && entry.Reward > 0 && entry.Power > 0)
            {
                var rewardEfficiency = entry.Reward / entry.Power;
                var peerEfficiencies = _entries
                    .Where(other =>
                        other.Kind == BalanceObjectKind.Npc &&
                        string.Equals(other.Group, entry.Group, StringComparison.OrdinalIgnoreCase) &&
                        other.Reward > 0 &&
                        other.Power > 0)
                    .Select(other => other.Reward / other.Power)
                    .ToArray();

                var rewardBaseline = Median(peerEfficiencies);
                if (rewardBaseline > 0)
                {
                    var rewardDeviation = (rewardEfficiency / rewardBaseline - 1d) * 100d;
                    if (Math.Abs(rewardDeviation) >= warning)
                    {
                        entry.Suggestion +=
                            $"\r\n\r\nReward check: EXP per combat-power is {Math.Abs(rewardDeviation):0.0}% " +
                            $"{(rewardDeviation > 0 ? "above" : "below")} same-level peers.";
                    }
                }
            }
        }
    }

    private void RefreshGrid()
    {
        var selectedScope = _scope.SelectedItem?.ToString() ?? "Overview";

        IEnumerable<BalanceEntry> visible = _entries;
        visible = selectedScope switch
        {
            "NPCs" => visible.Where(entry => entry.Kind == BalanceObjectKind.Npc),
            "Combat Simulation" => visible.Where(entry =>
                entry.Kind == BalanceObjectKind.Npc &&
                !string.IsNullOrWhiteSpace(entry.SimulationStatus)),
            "Equipment / Items" => visible.Where(entry => entry.Kind == BalanceObjectKind.Item),
            "Spells" => visible.Where(entry => entry.Kind == BalanceObjectKind.Spell),
            "Resources" => visible.Where(entry => entry.Kind == BalanceObjectKind.Resource),
            "Classes" => visible.Where(entry => entry.Kind == BalanceObjectKind.PlayerClass),
            _ => visible.Where(entry => entry.Severity != "OK"),
        };

        visible = selectedScope == "Combat Simulation"
            ? visible
                .OrderBy(entry => entry.SimulationStatus == "TOO HARD" ? 0 :
                                  entry.SimulationStatus == "TOO EASY" ? 1 : 2)
                .ThenByDescending(entry =>
                    Math.Abs((entry.SimulationHpLoss ?? 0d) - (double)_targetHpLoss.Value))
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            : visible
                .OrderBy(entry => entry.Severity == "CRITICAL" ? 0 : entry.Severity == "WARNING" ? 1 : 2)
                .ThenByDescending(entry => Math.Abs(entry.DeviationPercent))
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

        _grid.Rows.Clear();

        foreach (var entry in visible)
        {
            var rowIndex = _grid.Rows.Add(
                KindLabel(entry.Kind),
                entry.Name,
                entry.Group,
                entry.Power.ToString("0.0"),
                entry.Baseline.ToString("0.0"),
                $"{entry.DeviationPercent:+0.0;-0.0;0.0}%",
                entry.Severity,
                entry.SimulationTtk.HasValue ? $"{entry.SimulationTtk.Value:0.00}s" : "",
                entry.SimulationHpLoss.HasValue ? $"{entry.SimulationHpLoss.Value:0.0}%" : "",
                entry.SimulationStatus
            );

            var row = _grid.Rows[rowIndex];
            row.Tag = entry;

            if (entry.Severity == "CRITICAL")
            {
                row.DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(255, 135, 135);
            }
            else if (entry.Severity == "WARNING")
            {
                row.DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(255, 205, 120);
            }

            if (selectedScope == "Combat Simulation")
            {
                row.DefaultCellStyle.ForeColor = entry.SimulationStatus switch
                {
                    "TOO HARD" => System.Drawing.Color.FromArgb(255, 135, 135),
                    "TOO EASY" => System.Drawing.Color.FromArgb(130, 190, 255),
                    _ => System.Drawing.Color.FromArgb(155, 225, 165),
                };
            }
        }

        var critical = _entries.Count(entry => entry.Severity == "CRITICAL");
        var warnings = _entries.Count(entry => entry.Severity == "WARNING");
        var ok = _entries.Count(entry => entry.Severity == "OK");

        var simHard = _entries.Count(entry => entry.SimulationStatus == "TOO HARD");
        var simEasy = _entries.Count(entry => entry.SimulationStatus == "TOO EASY");
        var simTarget = _entries.Count(entry => entry.SimulationStatus == "TARGET");

        _summary.Text =
            $"Analyzed {_entries.Count:N0} objects - OK: {ok:N0}   Warning: {warnings:N0}   Critical: {critical:N0}" +
            $"   |   Simulation: Target {simTarget:N0} / Hard {simHard:N0} / Easy {simEasy:N0}";

        if (_grid.Rows.Count > 0)
        {
            _grid.Rows[0].Selected = true;
        }
        else
        {
            _details.Clear();
        }
    }

    private void ShowSelectedDetails()
    {
        if (_grid.SelectedRows.Count == 0 ||
            _grid.SelectedRows[0].Tag is not BalanceEntry entry)
        {
            _details.Clear();
            return;
        }

        _details.Text =
            $"{KindLabel(entry.Kind)}\r\n" +
            $"{entry.Name}\r\n" +
            $"{new string('=', Math.Min(42, Math.Max(8, entry.Name.Length)))}\r\n\r\n" +
            $"Peer group: {entry.Group}\r\n" +
            $"Status: {entry.Severity}\r\n" +
            $"Power score: {entry.Power:0.0}\r\n" +
            $"Peer baseline: {entry.Baseline:0.0}\r\n" +
            $"Deviation: {entry.DeviationPercent:+0.0;-0.0;0.0}%\r\n\r\n" +
            $"{entry.Metrics}\r\n\r\n" +
            (entry.SimulationTtk.HasValue
                ? $"COMBAT SIMULATION\r\n" +
                  $"-----------------\r\n" +
                  $"Status: {entry.SimulationStatus}\r\n" +
                  $"{entry.SimulationNotes}\r\n\r\n"
                : string.Empty) +
            $"SUGGESTION\r\n" +
            $"----------\r\n" +
            entry.Suggestion;
    }

    private void ShowSelectedProgression()
    {
        if (_grid.SelectedRows.Count == 0 ||
            _grid.SelectedRows[0].Tag is not BalanceEntry entry ||
            entry.Kind != BalanceObjectKind.Npc ||
            !NPCDescriptor.Lookup.TryGetValue(entry.Id, out var npc) ||
            npc == null)
        {
            MessageBox.Show(
                this,
                "Select an NPC in the Balance Lab first.",
                "Game Balance Lab",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        var selectedChoice = _simulationClass.SelectedItem as ClassChoice;
        var classes = new List<object>();

        if (selectedChoice?.Id is Guid selectedId &&
            ClassDescriptor.Lookup.TryGetValue(selectedId, out var selectedClass) &&
            selectedClass != null)
        {
            classes.Add(selectedClass);
        }
        else
        {
            classes.AddRange(
                ClassDescriptor.Lookup.Values
                    .Where(value => value != null)
                    .Cast<object>()
            );
        }

        if (classes.Count == 0)
        {
            return;
        }

        var partySize = Math.Clamp((int)_partySize.Value, 1, 5);
        var gearProfile = Math.Max(0, _gearProfile.SelectedIndex);
        var includeClassSpells = _includeClassSpells.Checked;
        var targetTtk = Math.Max(0.1, (double)_targetTtk.Value);
        var targetHpLoss = Math.Max(0.1, (double)_targetHpLoss.Value);
        var maxLevel = Math.Max(1, Options.Instance.Player.MaxLevel);

        var points = new List<ProgressionPoint>(maxLevel);

        for (var level = 1; level <= maxLevel; level++)
        {
            var simulations = classes
                .Select(
                    playerClass => SimulateClassVsNpc(
                        playerClass,
                        npc,
                        level,
                        partySize,
                        gearProfile,
                        includeClassSpells,
                        targetTtk
                    )
                )
                .Where(result => result != null)
                .Cast<CombatSimulation>()
                .ToArray();

            if (simulations.Length == 0)
            {
                continue;
            }

            points.Add(
                new ProgressionPoint
                {
                    Level = level,
                    Ttk = simulations.Average(result => result.TtkSeconds),
                    HpLoss = simulations.Average(result => result.HpLossPercent),
                    Dps = simulations.Average(result => result.PlayerDps) * partySize,
                }
            );
        }

        var simulationLabel =
            $"{(selectedChoice?.Id == null ? "Average all classes" : selectedChoice.Name)} | " +
            $"Party {partySize} | {_gearProfile.Text} | Spells {(includeClassSpells ? "ON" : "OFF")}";

        using var form = new ProgressionForm(
            entry.Name,
            simulationLabel,
            points,
            targetTtk,
            targetHpLoss
        );
        form.ShowDialog(this);
    }

    private void OpenSelected()
    {
        if (_openEditor == null ||
            _grid.SelectedRows.Count == 0 ||
            _grid.SelectedRows[0].Tag is not BalanceEntry entry)
        {
            return;
        }

        _openEditor(
            new BalanceOpenRequest
            {
                Kind = entry.Kind,
                Id = entry.Id,
            }
        );
    }

    private static string KindLabel(BalanceObjectKind kind)
    {
        return kind switch
        {
            BalanceObjectKind.Npc => "NPC",
            BalanceObjectKind.Item => "Item",
            BalanceObjectKind.Spell => "Spell",
            BalanceObjectKind.Resource => "Resource",
            BalanceObjectKind.PlayerClass => "Class",
            _ => kind.ToString(),
        };
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Where(value => !double.IsNaN(value) && !double.IsInfinity(value))
            .OrderBy(value => value)
            .ToArray();

        if (sorted.Length == 0)
        {
            return 0;
        }

        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2d
            : sorted[middle];
    }

    private static object? Property(object source, string name)
    {
        return source.GetType().GetProperty(name)?.GetValue(source);
    }

    private static string Text(object source, string name, string fallback)
    {
        return Property(source, name)?.ToString() ?? fallback;
    }

    private static double Number(object source, string name)
    {
        return ToDouble(Property(source, name));
    }

    private static double Indexed(object source, string name, int index)
    {
        var value = Property(source, name);
        if (value is IList list && index >= 0 && index < list.Count)
        {
            return ToDouble(list[index]);
        }

        if (value is Array array && index >= 0 && index < array.Length)
        {
            return ToDouble(array.GetValue(index));
        }

        return 0d;
    }

    private static Guid GuidValue(object source, string name)
    {
        var value = Property(source, name);
        if (value is Guid guid)
        {
            return guid;
        }

        return Guid.TryParse(value?.ToString(), out var parsed)
            ? parsed
            : Guid.Empty;
    }

    private static double ToDouble(object? value)
    {
        if (value == null)
        {
            return 0d;
        }

        try
        {
            return Convert.ToDouble(value);
        }
        catch
        {
            return 0d;
        }
    }
}
