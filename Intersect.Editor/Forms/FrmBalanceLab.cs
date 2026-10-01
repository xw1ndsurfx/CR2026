using System.Collections;
using DarkUI.Forms;
using Intersect.Editor.Core;
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
    }

    private readonly Action<BalanceOpenRequest>? _openEditor;

    private readonly ComboBox _profile = new();
    private readonly NumericUpDown _warningThreshold = new();
    private readonly NumericUpDown _criticalThreshold = new();
    private readonly ListBox _scope = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _details = new();
    private readonly Label _summary = new();

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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

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
        root.Controls.Add(toolbar, 0, 0);

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

        var openButton = CreateAccentButton("OPEN SELECTED IN EDITOR");
        openButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        openButton.Location = new Point(1160, 7);
        openButton.Size = new Size(270, 32);
        openButton.Click += (_, _) => OpenSelected();
        footer.Controls.Add(openButton);
        footer.Resize += (_, _) =>
        {
            openButton.Left = Math.Max(10, footer.ClientSize.Width - openButton.Width - 14);
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

    private void RunAnalysis()
    {
        _entries.Clear();

        AnalyzeNpcs();
        AnalyzeItems();
        AnalyzeSpells();
        AnalyzeResources();
        AnalyzeClasses();

        ApplyBaselinesAndSeverity();
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
            "Equipment / Items" => visible.Where(entry => entry.Kind == BalanceObjectKind.Item),
            "Spells" => visible.Where(entry => entry.Kind == BalanceObjectKind.Spell),
            "Resources" => visible.Where(entry => entry.Kind == BalanceObjectKind.Resource),
            "Classes" => visible.Where(entry => entry.Kind == BalanceObjectKind.PlayerClass),
            _ => visible.Where(entry => entry.Severity != "OK"),
        };

        visible = visible
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
                entry.Severity
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
        }

        var critical = _entries.Count(entry => entry.Severity == "CRITICAL");
        var warnings = _entries.Count(entry => entry.Severity == "WARNING");
        var ok = _entries.Count(entry => entry.Severity == "OK");

        _summary.Text =
            $"Analyzed {_entries.Count:N0} objects - OK: {ok:N0}   Warning: {warnings:N0}   Critical: {critical:N0}";

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
            $"SUGGESTION\r\n" +
            $"----------\r\n" +
            entry.Suggestion;
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
