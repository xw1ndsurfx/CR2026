using DarkUI.Forms;
using Intersect.Editor.Content;
using Intersect.Editor.Networking;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.WorldEvents.Invasions;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmInvasionConfiguration : DarkForm
{
    private sealed record Choice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly List<InvasionDefinition> _invasions;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };

    private readonly TextBox _name = new() { Width = 320 };
    private readonly CheckBox _enabled = new() { Text = "Enabled", AutoSize = true };
    private readonly CheckedListBox _days = new() { Height = 88, CheckOnClick = true };
    private readonly NumericUpDown _hour = new() { Minimum = 0, Maximum = 23, Width = 70 };
    private readonly NumericUpDown _minute = new() { Minimum = 0, Maximum = 59, Width = 70 };

    private readonly ComboBox _targetMap = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly ComboBox _targetEvent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly NumericUpDown _targetX = new() { Minimum = 0, Maximum = 255, Width = 75 };
    private readonly NumericUpDown _targetY = new() { Minimum = 0, Maximum = 255, Width = 75 };
    private readonly NumericUpDown _targetHealth = new() { Minimum = 1, Maximum = 1_000_000_000, Width = 130 };
    private readonly NumericUpDown _hitInterval = new() { Minimum = 250, Maximum = 60_000, Increment = 250, Width = 110 };
    private readonly NumericUpDown _rewardExp = new() { Minimum = 0, Maximum = 2_000_000_000, Width = 150 };
    private readonly NumericUpDown _participationMinimumRewardPercent = new() { Minimum = 0, Maximum = 500, Width = 90 };
    private readonly NumericUpDown _participationMaximumRewardPercent = new() { Minimum = 1, Maximum = 500, Width = 90 };
    private readonly NumericUpDown _healingContributionPercent = new() { Minimum = 0, Maximum = 500, Width = 90 };

    private readonly ComboBox _invasionMusic = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 330 };
    private readonly NumericUpDown _nightBrightness = new() { Minimum = 0, Maximum = 100, Width = 90 };
    private readonly NumericUpDown _overlayAlpha = new() { Minimum = 0, Maximum = 255, Width = 80 };
    private readonly NumericUpDown _overlayRed = new() { Minimum = 0, Maximum = 255, Width = 80 };
    private readonly NumericUpDown _overlayGreen = new() { Minimum = 0, Maximum = 255, Width = 80 };
    private readonly NumericUpDown _overlayBlue = new() { Minimum = 0, Maximum = 255, Width = 80 };
    private readonly ComboBox _fog = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 330 };
    private readonly NumericUpDown _fogAlpha = new() { Minimum = 0, Maximum = 255, Width = 80 };
    private readonly NumericUpDown _fogXSpeed = new() { Minimum = -5, Maximum = 5, Width = 80 };
    private readonly NumericUpDown _fogYSpeed = new() { Minimum = -5, Maximum = 5, Width = 80 };
    private readonly CheckBox _environmentOutdoorsOnly = new()
    {
        Text = "Only apply night / overlay / fog on outdoor maps",
        AutoSize = true,
    };

    private readonly CheckBox _scaleNpcToPlayers = new()
    {
        Text = "Scale invasion NPCs to connected players",
        AutoSize = true,
    };
    private readonly NumericUpDown _scalingMinimumLevel = new() { Minimum = 1, Maximum = 1_000, Width = 90 };
    private readonly NumericUpDown _scalingMaximumLevel = new() { Minimum = 1, Maximum = 1_000, Width = 90 };
    private readonly NumericUpDown _scalingLevelOffset = new() { Minimum = -1_000, Maximum = 1_000, Width = 90 };
    private readonly NumericUpDown _extraPlayerHealthPercent = new() { Minimum = 0, Maximum = 500, Width = 90 };
    private readonly NumericUpDown _bossHealthPercent = new() { Minimum = 1, Maximum = 2_000, Width = 90 };
    private readonly NumericUpDown _bossDamagePercent = new() { Minimum = 1, Maximum = 1_000, Width = 90 };

    private readonly ListBox _waves = new() { Dock = DockStyle.Fill };

    private int _selectedIndex = -1;
    private bool _loading;

    public FrmInvasionConfiguration()
    {
        Text = "Events - Invasions";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1040;
        Height = 720;
        MinimizeBox = false;

        _invasions = InvasionConfiguration.FromJson(InvasionConfiguration.Instance.ToJson())
            .Invasions.ToList();

        FillMaps();
        FillEnvironmentAssets();
        BuildUi();
        _scaleNpcToPlayers.CheckedChanged += (_, _) => RefreshScalingControls();
        _targetMap.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            var mapId = (_targetMap.SelectedItem as Choice)?.Id ?? Guid.Empty;
            FillTargetEvents(mapId, Guid.Empty);
            ApplySelectedTargetEvent();
        };
        _targetEvent.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            ApplySelectedTargetEvent();
        };
        RefreshInvasionList();

        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;
    }

    private void FillMaps()
    {
        _targetMap.Items.Clear();
        var names = GameObjectType.Map.Names();
        for (var index = 0; index < names.Length; ++index)
            _targetMap.Items.Add(new Choice(GameObjectType.Map.IdFromList(index), names[index]));
    }

    private void FillEnvironmentAssets()
    {
        _invasionMusic.Items.Clear();
        _invasionMusic.Items.Add(string.Empty);
        foreach (var music in GameContentManager.SmartSortedMusicNames)
            _invasionMusic.Items.Add(music);

        _fog.Items.Clear();
        _fog.Items.Add(string.Empty);
        foreach (var fog in GameContentManager.GetSmartSortedTextureNames(GameContentManager.TextureType.Fog))
            _fog.Items.Add(fog);
    }

    private void FillTargetEvents(Guid mapId, Guid selectedEventId)
    {
        var wasLoading = _loading;
        _loading = true;
        _targetEvent.Items.Clear();
        _targetEvent.Items.Add(new Choice(Guid.Empty, "None - use target tile"));

        var map = MapDescriptor.Get(mapId);
        if (map != null)
        {
            foreach (var targetEvent in map.LocalEvents.Values
                         .Where(targetEvent => targetEvent != null && !targetEvent.CommonEvent && targetEvent.Global)
                         .OrderBy(targetEvent => targetEvent.Name, StringComparer.OrdinalIgnoreCase))
            {
                _targetEvent.Items.Add(
                    new Choice(
                        targetEvent.Id,
                        $"{targetEvent.Name} ({targetEvent.SpawnX},{targetEvent.SpawnY})"
                    )
                );
            }
        }

        SelectChoice(_targetEvent, selectedEventId);
        _loading = wasLoading;
    }

    private void ApplySelectedTargetEvent()
    {
        var eventId = (_targetEvent.SelectedItem as Choice)?.Id ?? Guid.Empty;
        var targetEvent = eventId == Guid.Empty ? null : EventDescriptor.Get(eventId);
        var usesEvent = targetEvent != null;

        if (usesEvent)
        {
            _targetX.Value = Math.Clamp(targetEvent!.SpawnX, (int)_targetX.Minimum, (int)_targetX.Maximum);
            _targetY.Value = Math.Clamp(targetEvent.SpawnY, (int)_targetY.Minimum, (int)_targetY.Maximum);
        }

        _targetX.Enabled = !usesEvent;
        _targetY.Enabled = !usesEvent;
    }

    private void BuildUi()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 250,
            FixedPanel = FixedPanel.Panel1,
        };

        var leftButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 76,
            Padding = new Padding(4),
        };
        leftButtons.Controls.Add(Button("Add", (_, _) => AddInvasion()));
        leftButtons.Controls.Add(Button("Duplicate", (_, _) => DuplicateInvasion()));
        leftButtons.Controls.Add(Button("Delete", (_, _) => DeleteInvasion()));
        split.Panel1.Controls.Add(_list);
        split.Panel1.Controls.Add(leftButtons);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildEnvironmentTab());
        tabs.TabPages.Add(BuildScalingTab());
        tabs.TabPages.Add(BuildWavesTab());
        split.Panel2.Controls.Add(tabs);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        footer.Controls.Add(Button("Close", (_, _) => Close()));
        footer.Controls.Add(Button("Save", (_, _) => SaveConfiguration()));
        footer.Controls.Add(Button("Start Now", (_, _) => StartNow()));

        Controls.Add(split);
        Controls.Add(footer);

        _list.SelectedIndexChanged += (_, _) => SelectInvasion(_list.SelectedIndex);
    }

    private TabPage BuildGeneralTab()
    {
        var page = new TabPage("General");
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(14),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(table, "Name", _name);
        AddRow(table, "State", _enabled);

        _days.Items.AddRange(new object[]
        {
            "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday",
        });
        AddRow(table, "Days", _days);

        var time = new FlowLayoutPanel { AutoSize = true };
        time.Controls.Add(_hour);
        time.Controls.Add(new Label { Text = ":", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        time.Controls.Add(_minute);
        time.Controls.Add(new Label
        {
            Text = "Server local time",
            AutoSize = true,
            Padding = new Padding(8, 5, 0, 0),
        });
        AddRow(table, "Start time", time);

        AddRow(table, "Target island / map", _targetMap);
        AddRow(table, "Defense target event", _targetEvent);

        var targetCoords = new FlowLayoutPanel { AutoSize = true };
        targetCoords.Controls.Add(new Label { Text = "X", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        targetCoords.Controls.Add(_targetX);
        targetCoords.Controls.Add(new Label { Text = "Y", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        targetCoords.Controls.Add(_targetY);
        AddRow(table, "Defense target tile", targetCoords);

        AddRow(table, "Target HP", _targetHealth);
        AddRow(table, "Invader hit interval (ms)", _hitInterval);
        AddRow(table, "Base victory EXP", _rewardExp);

        var effortReward = new FlowLayoutPanel { AutoSize = true };
        effortReward.Controls.Add(new Label { Text = "Min %", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        effortReward.Controls.Add(_participationMinimumRewardPercent);
        effortReward.Controls.Add(new Label { Text = "Max %", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        effortReward.Controls.Add(_participationMaximumRewardPercent);
        AddRow(table, "Effort reward range", effortReward);
        AddRow(table, "Healing contribution weight (%)", _healingContributionPercent);

        var help = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(600, 0),
            Text =
                "Choose a map event to make that event the invasion objective. Its map position becomes the target automatically. " +
                "Choose None to use the manual target tile instead. Invasion NPCs ignore their normal idle movement while assigned to an invasion and march toward this objective. " +
                "Defender effort includes actual damage dealt to invasion NPCs plus effective healing done to players already participating in the invasion. Overhealing does not count. " +
                "Average effort receives 100% of the base EXP, while lower or higher effort scales within the configured min/max range.",
        };
        AddRow(table, "How it works", help);

        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildEnvironmentTab()
    {
        var page = new TabPage("Environment");
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(14),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(table, "Invasion music", _invasionMusic);
        AddRow(table, "Night brightness (%)", _nightBrightness);

        var overlay = new FlowLayoutPanel { AutoSize = true };
        overlay.Controls.Add(new Label { Text = "A", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        overlay.Controls.Add(_overlayAlpha);
        overlay.Controls.Add(new Label { Text = "R", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        overlay.Controls.Add(_overlayRed);
        overlay.Controls.Add(new Label { Text = "G", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        overlay.Controls.Add(_overlayGreen);
        overlay.Controls.Add(new Label { Text = "B", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        overlay.Controls.Add(_overlayBlue);
        AddRow(table, "Overlay ARGB", overlay);

        AddRow(table, "Fog texture", _fog);

        var fogSettings = new FlowLayoutPanel { AutoSize = true };
        fogSettings.Controls.Add(new Label { Text = "Alpha", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        fogSettings.Controls.Add(_fogAlpha);
        fogSettings.Controls.Add(new Label { Text = "X speed", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        fogSettings.Controls.Add(_fogXSpeed);
        fogSettings.Controls.Add(new Label { Text = "Y speed", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        fogSettings.Controls.Add(_fogYSpeed);
        AddRow(table, "Fog settings", fogSettings);

        AddRow(table, "Scope", _environmentOutdoorsOnly);

        var help = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(650, 0),
            Text =
                "During the invasion, the selected music overrides map/event music everywhere. " +
                "Outdoor maps use the configured night brightness, color overlay and fog. " +
                "When the invasion ends, the current map's normal music, daylight tint, overlay and fog resume automatically.",
        };
        AddRow(table, "Behavior", help);

        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildScalingTab()
    {
        var page = new TabPage("NPC Scaling");
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(14),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(table, "Adaptive scaling", _scaleNpcToPlayers);

        var levels = new FlowLayoutPanel { AutoSize = true };
        levels.Controls.Add(new Label { Text = "Min", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        levels.Controls.Add(_scalingMinimumLevel);
        levels.Controls.Add(new Label { Text = "Max", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        levels.Controls.Add(_scalingMaximumLevel);
        levels.Controls.Add(new Label { Text = "Offset", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        levels.Controls.Add(_scalingLevelOffset);
        AddRow(table, "NPC target level", levels);

        AddRow(table, "Extra HP / extra player (%)", _extraPlayerHealthPercent);
        AddRow(table, "Boss HP (%)", _bossHealthPercent);
        AddRow(table, "Boss damage (%)", _bossDamagePercent);

        var help = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(650, 0),
            Text =
                "At the start of every wave, the server takes a fresh snapshot of connected players and uses their median level. " +
                "Only NPC instances spawned by this invasion are scaled; the NPC definition and every normal NPC elsewhere in the world stay unchanged. " +
                "HP also grows with the number of connected players. Players joining later affect the next wave, not enemies already in combat.",
        };
        AddRow(table, "Behavior", help);

        page.Controls.Add(table);
        return page;
    }

    private void RefreshScalingControls()
    {
        var enabled = _scaleNpcToPlayers.Checked;
        _scalingMinimumLevel.Enabled = enabled;
        _scalingMaximumLevel.Enabled = enabled;
        _scalingLevelOffset.Enabled = enabled;
        _extraPlayerHealthPercent.Enabled = enabled;
        _bossHealthPercent.Enabled = enabled;
        _bossDamagePercent.Enabled = enabled;
    }

    private TabPage BuildWavesTab()
    {
        var page = new TabPage("Waves");
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            Padding = new Padding(6),
        };
        buttons.Controls.Add(Button("Add Wave", (_, _) => AddWave()));
        buttons.Controls.Add(Button("Edit Wave", (_, _) => EditWave()));
        buttons.Controls.Add(Button("Remove Wave", (_, _) => RemoveWave()));

        var help = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = "Waves launch in order. Delay is counted after the previous wave is cleared. Mark any NPC spawn as Boss to create a boss wave.",
            Padding = new Padding(8),
        };

        page.Controls.Add(_waves);
        page.Controls.Add(buttons);
        page.Controls.Add(help);
        return page;
    }

    private void SelectInvasion(int index)
    {
        if (_loading) return;
        PersistCurrent();

        _selectedIndex = index;
        if (index < 0 || index >= _invasions.Count)
        {
            RefreshWaveList();
            return;
        }

        _loading = true;
        var invasion = _invasions[index];
        _name.Text = invasion.Name;
        _enabled.Checked = invasion.Enabled;
        _hour.Value = invasion.StartHour;
        _minute.Value = invasion.StartMinute;
        _targetX.Value = invasion.TargetX;
        _targetY.Value = invasion.TargetY;
        _targetHealth.Value = invasion.TargetHealth;
        _hitInterval.Value = invasion.ObjectiveHitIntervalMs;
        _rewardExp.Value = invasion.RewardExperience;
        _participationMinimumRewardPercent.Value = invasion.ParticipationMinimumRewardPercent;
        _participationMaximumRewardPercent.Value = invasion.ParticipationMaximumRewardPercent;
        _healingContributionPercent.Value = invasion.HealingContributionPercent;
        _invasionMusic.Text = invasion.InvasionMusic ?? string.Empty;
        _nightBrightness.Value = invasion.NightBrightness;
        _overlayAlpha.Value = invasion.OverlayAlpha;
        _overlayRed.Value = invasion.OverlayRed;
        _overlayGreen.Value = invasion.OverlayGreen;
        _overlayBlue.Value = invasion.OverlayBlue;
        _fog.Text = invasion.Fog ?? string.Empty;
        _fogAlpha.Value = invasion.FogAlpha;
        _fogXSpeed.Value = invasion.FogXSpeed;
        _fogYSpeed.Value = invasion.FogYSpeed;
        _environmentOutdoorsOnly.Checked = invasion.EnvironmentOutdoorsOnly;
        _scaleNpcToPlayers.Checked = invasion.ScaleNpcToPlayers;
        _scalingMinimumLevel.Value = invasion.ScalingMinimumLevel;
        _scalingMaximumLevel.Value = invasion.ScalingMaximumLevel;
        _scalingLevelOffset.Value = invasion.ScalingLevelOffset;
        _extraPlayerHealthPercent.Value = invasion.ExtraPlayerHealthPercent;
        _bossHealthPercent.Value = invasion.BossHealthPercent;
        _bossDamagePercent.Value = invasion.BossDamagePercent;
        RefreshScalingControls();

        for (var day = 0; day < _days.Items.Count; ++day)
        {
            var flag = (InvasionScheduleDays)(1 << day);
            _days.SetItemChecked(day, (invasion.ScheduleDays & flag) != 0);
        }

        SelectChoice(_targetMap, invasion.TargetMapId);
        FillTargetEvents(invasion.TargetMapId, invasion.TargetEventId);
        ApplySelectedTargetEvent();
        RefreshWaveList();
        _loading = false;
    }

    private void PersistCurrent()
    {
        if (_loading || _selectedIndex < 0 || _selectedIndex >= _invasions.Count)
            return;

        var invasion = _invasions[_selectedIndex];
        invasion.Name = string.IsNullOrWhiteSpace(_name.Text) ? "Invasion" : _name.Text.Trim();
        invasion.Enabled = _enabled.Checked;
        invasion.StartHour = (int)_hour.Value;
        invasion.StartMinute = (int)_minute.Value;
        invasion.TargetMapId = (_targetMap.SelectedItem as Choice)?.Id ?? Guid.Empty;
        invasion.TargetEventId = (_targetEvent.SelectedItem as Choice)?.Id ?? Guid.Empty;
        invasion.TargetX = (int)_targetX.Value;
        invasion.TargetY = (int)_targetY.Value;
        invasion.TargetHealth = (int)_targetHealth.Value;
        invasion.ObjectiveHitIntervalMs = (int)_hitInterval.Value;
        invasion.InvasionMusic = _invasionMusic.Text?.Trim() ?? string.Empty;
        invasion.NightBrightness = (int)_nightBrightness.Value;
        invasion.OverlayAlpha = (int)_overlayAlpha.Value;
        invasion.OverlayRed = (int)_overlayRed.Value;
        invasion.OverlayGreen = (int)_overlayGreen.Value;
        invasion.OverlayBlue = (int)_overlayBlue.Value;
        invasion.Fog = _fog.Text?.Trim() ?? string.Empty;
        invasion.FogAlpha = (int)_fogAlpha.Value;
        invasion.FogXSpeed = (int)_fogXSpeed.Value;
        invasion.FogYSpeed = (int)_fogYSpeed.Value;
        invasion.EnvironmentOutdoorsOnly = _environmentOutdoorsOnly.Checked;
        invasion.ScaleNpcToPlayers = _scaleNpcToPlayers.Checked;
        invasion.ScalingMinimumLevel = (int)_scalingMinimumLevel.Value;
        invasion.ScalingMaximumLevel = (int)_scalingMaximumLevel.Value;
        invasion.ScalingLevelOffset = (int)_scalingLevelOffset.Value;
        invasion.ExtraPlayerHealthPercent = (int)_extraPlayerHealthPercent.Value;
        invasion.BossHealthPercent = (int)_bossHealthPercent.Value;
        invasion.BossDamagePercent = (int)_bossDamagePercent.Value;
        invasion.ParticipationMinimumRewardPercent = (int)_participationMinimumRewardPercent.Value;
        invasion.ParticipationMaximumRewardPercent = (int)_participationMaximumRewardPercent.Value;
        invasion.HealingContributionPercent = (int)_healingContributionPercent.Value;
        invasion.RewardExperience = (long)_rewardExp.Value;

        var days = InvasionScheduleDays.None;
        for (var day = 0; day < _days.Items.Count; ++day)
        {
            if (_days.GetItemChecked(day))
                days |= (InvasionScheduleDays)(1 << day);
        }
        invasion.ScheduleDays = days;

        RefreshInvasionList(keepIndex: _selectedIndex);
    }

    private void AddInvasion()
    {
        PersistCurrent();
        var defaultMap = (_targetMap.Items.Cast<Choice>().FirstOrDefault())?.Id ?? Guid.Empty;
        _invasions.Add(
            new InvasionDefinition
            {
                Name = $"Invasion {_invasions.Count + 1}",
                TargetMapId = defaultMap,
                Waves =
                [
                    new InvasionWaveDefinition
                    {
                        Name = "Wave 1",
                        Spawns = [],
                    },
                ],
            }
        );
        RefreshInvasionList(keepIndex: _invasions.Count - 1);
    }

    private void DuplicateInvasion()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _invasions.Count) return;
        PersistCurrent();

        var source = _invasions[_selectedIndex];
        var clone = InvasionConfiguration.FromJson(
            new InvasionConfiguration { Invasions = [source] }.ToJson()
        ).Invasions[0];
        clone.Id = Guid.NewGuid();
        clone.Name += " Copy";
        foreach (var wave in clone.Waves)
            wave.Id = Guid.NewGuid();

        _invasions.Add(clone);
        RefreshInvasionList(keepIndex: _invasions.Count - 1);
    }

    private void DeleteInvasion()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _invasions.Count) return;
        if (MessageBox.Show(
                "Delete this invasion?",
                "Invasions",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            ) != DialogResult.Yes)
            return;

        _invasions.RemoveAt(_selectedIndex);
        _selectedIndex = -1;
        RefreshInvasionList(keepIndex: Math.Min(_list.Items.Count - 1, _invasions.Count - 1));
    }

    private void AddWave()
    {
        if (!TryCurrent(out var invasion)) return;
        PersistCurrent();

        var wave = new InvasionWaveDefinition
        {
            Name = $"Wave {invasion.Waves.Length + 1}",
            DelaySeconds = invasion.Waves.Length == 0 ? 5 : 10,
            Spawns = [],
        };

        using var dialog = new InvasionWaveDialog(wave, invasion.TargetMapId);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        invasion.Waves = [.. invasion.Waves, dialog.Result];
        RefreshWaveList();
    }

    private void EditWave()
    {
        if (!TryCurrent(out var invasion) ||
            _waves.SelectedIndex < 0 ||
            _waves.SelectedIndex >= invasion.Waves.Length)
            return;

        using var dialog = new InvasionWaveDialog(invasion.Waves[_waves.SelectedIndex], invasion.TargetMapId);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        invasion.Waves[_waves.SelectedIndex] = dialog.Result;
        RefreshWaveList();
    }

    private void RemoveWave()
    {
        if (!TryCurrent(out var invasion) ||
            _waves.SelectedIndex < 0 ||
            _waves.SelectedIndex >= invasion.Waves.Length)
            return;

        invasion.Waves = invasion.Waves
            .Where((_, index) => index != _waves.SelectedIndex)
            .ToArray();
        RefreshWaveList();
    }

    private void RefreshWaveList()
    {
        _waves.Items.Clear();
        if (!TryCurrent(out var invasion)) return;

        for (var index = 0; index < invasion.Waves.Length; ++index)
        {
            var wave = invasion.Waves[index];
            var total = wave.Spawns.Sum(spawn => spawn.Count);
            var bosses = wave.Spawns.Where(spawn => spawn.IsBoss).Sum(spawn => spawn.Count);
            _waves.Items.Add(
                $"{index + 1}. {wave.Name} | delay {wave.DelaySeconds}s | {total} NPC(s)" +
                (bosses > 0 ? $" | {bosses} boss(es)" : string.Empty)
            );
        }
    }

    private void SaveConfiguration()
    {
        PersistCurrent();
        var configuration = new InvasionConfiguration { Invasions = _invasions.ToArray() };
        if (!configuration.IsStructurallyValid)
        {
            MessageBox.Show(
                "The invasion configuration is incomplete. Every invasion needs at least one wave, and every wave needs at least one NPC spawn. Select at least one schedule day and a target map.",
                "Invasions",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        PacketSender.SendSaveInvasionConfiguration(configuration.ToJson());
        InvasionConfiguration.Load(configuration.ToJson());
        MessageBox.Show("Invasion configuration sent to the server.", "Invasions");
    }

    private void StartNow()
    {
        PersistCurrent();
        if (!TryCurrent(out var invasion)) return;

        // Save first so the runtime starts exactly what is visible in the editor.
        var configuration = new InvasionConfiguration { Invasions = _invasions.ToArray() };
        if (!configuration.IsStructurallyValid)
        {
            MessageBox.Show("Save a valid invasion before starting it.", "Invasions");
            return;
        }

        PacketSender.SendSaveInvasionConfiguration(configuration.ToJson());
        PacketSender.SendStartInvasionNow(invasion.Id);
    }

    private bool TryCurrent(out InvasionDefinition invasion)
    {
        if (_selectedIndex >= 0 && _selectedIndex < _invasions.Count)
        {
            invasion = _invasions[_selectedIndex];
            return true;
        }

        invasion = null!;
        return false;
    }

    private void RefreshInvasionList(int keepIndex = -1)
    {
        _loading = true;
        _list.Items.Clear();
        foreach (var invasion in _invasions)
            _list.Items.Add($"{(invasion.Enabled ? "●" : "○")} {invasion.Name}  {invasion.StartHour:00}:{invasion.StartMinute:00}");

        if (_list.Items.Count > 0)
            _list.SelectedIndex = Math.Clamp(keepIndex < 0 ? _selectedIndex : keepIndex, 0, _list.Items.Count - 1);
        _loading = false;

        if (_list.SelectedIndex >= 0 && _list.SelectedIndex != _selectedIndex)
            SelectInvasion(_list.SelectedIndex);
    }

    private static void SelectChoice(ComboBox combo, Guid id)
    {
        for (var index = 0; index < combo.Items.Count; ++index)
        {
            if ((combo.Items[index] as Choice)?.Id == id)
            {
                combo.SelectedIndex = index;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static Button Button(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += click;
        return button;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(
            new Label
            {
                Text = label,
                AutoSize = true,
                Padding = new Padding(0, 6, 8, 0),
            },
            0,
            row
        );
        table.Controls.Add(control, 1, row);
    }
}
