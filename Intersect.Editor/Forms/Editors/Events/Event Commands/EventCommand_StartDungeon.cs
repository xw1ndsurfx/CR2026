using DarkUI.Controls;
using Intersect.Editor.Localization;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.MapList;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;

public sealed class EventCommand_StartDungeon : UserControl
{
    private sealed record Choice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly FrmEvent _eventEditor;
    private readonly StartDungeonCommand _command;

    private readonly DarkComboBox _dungeon = Combo();
    private readonly DarkGroupBox _warpGroup = Group("Warp");
    private readonly DarkComboBox _map = Combo();
    private readonly DarkNumericUpDown _x = Numeric();
    private readonly DarkNumericUpDown _y = Numeric();
    private readonly DarkComboBox _direction = Combo();
    private readonly CheckBox _changeInstance = new()
    {
        AutoSize = true,
        ForeColor = System.Drawing.Color.Gainsboro,
    };
    private readonly DarkGroupBox _instanceGroup = Group("Instance Settings");
    private readonly DarkComboBox _instanceType = Combo();
    private readonly Label _xLabel = Label("X: 0");
    private readonly Label _yLabel = Label("Y: 0");

    public EventCommand_StartDungeon(StartDungeonCommand command, FrmEvent eventEditor)
    {
        _command = command;
        _eventEditor = eventEditor;

        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
        ForeColor = System.Drawing.Color.Gainsboro;
        Margin = new Padding(4, 3, 4, 3);
        Name = nameof(EventCommand_StartDungeon);
        Size = new Size(470, 306);

        PopulateChoices();
        BuildUi();
        LoadValues();
    }

    private static DarkComboBox Combo() =>
        new()
        {
            BackColor = System.Drawing.Color.FromArgb(69, 73, 74),
            BorderColor = System.Drawing.Color.FromArgb(90, 90, 90),
            BorderStyle = ButtonBorderStyle.Solid,
            ButtonColor = System.Drawing.Color.FromArgb(43, 43, 43),
            DrawDropdownHoverOutline = false,
            DrawFocusRectangle = false,
            DrawMode = DrawMode.OwnerDrawFixed,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            ForeColor = System.Drawing.Color.Gainsboro,
            FormattingEnabled = true,
            TextPadding = new Padding(2),
        };

    private static DarkNumericUpDown Numeric() =>
        new()
        {
            BackColor = System.Drawing.Color.FromArgb(69, 73, 74),
            ForeColor = System.Drawing.Color.Gainsboro,
            Minimum = 0,
        };

    private static DarkGroupBox Group(string text) =>
        new()
        {
            BackColor = System.Drawing.Color.FromArgb(60, 63, 65),
            BorderColor = System.Drawing.Color.FromArgb(90, 90, 90),
            ForeColor = System.Drawing.Color.Gainsboro,
            Text = text,
        };

    private static Label Label(string text) =>
        new()
        {
            AutoSize = true,
            ForeColor = System.Drawing.Color.Gainsboro,
            Text = text,
        };

    private void PopulateChoices()
    {
        foreach (var dungeon in DungeonConfiguration.Instance.Dungeons
                     .OrderBy(dungeon => dungeon.SortOrder)
                     .ThenBy(dungeon => dungeon.Rank)
                     .ThenBy(dungeon => dungeon.Name))
        {
            _dungeon.Items.Add(new Choice(dungeon.Id, $"[{dungeon.Rank}] {dungeon.Name}"));
        }

        foreach (var map in MapList.OrderedMaps)
            _map.Items.Add(new Choice(map.MapId, map.Name));

        _direction.Items.Clear();
        for (var i = -1; i < 4; ++i)
            _direction.Items.Add(Strings.Direction.dir[(Direction)i]);

        _instanceType.Items.Clear();
        foreach (MapInstanceType type in Enum.GetValues(typeof(MapInstanceType)))
            _instanceType.Items.Add(Strings.MapInstance.InstanceTypes[type]);
    }

    private void BuildUi()
    {
        var dungeonLabel = Label("Dungeon:");
        dungeonLabel.Location = new Point(15, 14);

        _dungeon.Location = new Point(88, 10);
        _dungeon.Size = new Size(344, 24);

        _warpGroup.Location = new Point(4, 43);
        _warpGroup.Size = new Size(448, 232);
        _warpGroup.Padding = new Padding(4, 3, 4, 3);

        var mapLabel = Label(Strings.Warping.map.ToString(""));
        mapLabel.Location = new Point(10, 22);

        _map.Location = new Point(54, 18);
        _map.Size = new Size(146, 24);

        _xLabel.Location = new Point(10, 53);
        _x.Location = new Point(54, 51);
        _x.Size = new Size(146, 23);
        _x.Maximum = Options.Instance.Map.MapWidth - 1;
        _x.ValueChanged += (_, _) => UpdateCoordinateLabels();

        _yLabel.Location = new Point(10, 84);
        _y.Location = new Point(54, 82);
        _y.Size = new Size(146, 23);
        _y.Maximum = Options.Instance.Map.MapHeight - 1;
        _y.ValueChanged += (_, _) => UpdateCoordinateLabels();

        var directionLabel = Label(Strings.Warping.direction.ToString(""));
        directionLabel.Location = new Point(10, 118);

        _direction.Location = new Point(54, 115);
        _direction.Size = new Size(146, 24);

        _changeInstance.Text = Strings.Warping.ChangeInstance;
        _changeInstance.Location = new Point(214, 22);
        _changeInstance.CheckedChanged += (_, _) =>
            _instanceGroup.Visible = _changeInstance.Checked;

        _instanceGroup.Text = Strings.Warping.MapInstancingGroup;
        _instanceGroup.Location = new Point(214, 59);
        _instanceGroup.Size = new Size(220, 118);

        var instanceLabel = Label(Strings.Warping.InstanceType);
        instanceLabel.Location = new Point(7, 36);

        _instanceType.Location = new Point(10, 54);
        _instanceType.Size = new Size(202, 24);

        var visual = new DarkButton
        {
            Location = new Point(14, 150),
            Size = new Size(187, 27),
            Padding = new Padding(6),
            Text = Strings.Warping.visual,
        };
        visual.Click += (_, _) => OpenVisualInterface();

        var save = new DarkButton
        {
            Location = new Point(245, 194),
            Size = new Size(88, 27),
            Padding = new Padding(6),
            Text = Strings.EventWarp.okay,
        };
        save.Click += (_, _) => Save();

        var cancel = new DarkButton
        {
            Location = new Point(340, 194),
            Size = new Size(88, 27),
            Padding = new Padding(6),
            Text = Strings.EventWarp.cancel,
        };
        cancel.Click += (_, _) => _eventEditor.CancelCommandEdit();

        _instanceGroup.Controls.Add(instanceLabel);
        _instanceGroup.Controls.Add(_instanceType);

        _warpGroup.Controls.Add(mapLabel);
        _warpGroup.Controls.Add(_map);
        _warpGroup.Controls.Add(_xLabel);
        _warpGroup.Controls.Add(_x);
        _warpGroup.Controls.Add(_yLabel);
        _warpGroup.Controls.Add(_y);
        _warpGroup.Controls.Add(directionLabel);
        _warpGroup.Controls.Add(_direction);
        _warpGroup.Controls.Add(_changeInstance);
        _warpGroup.Controls.Add(_instanceGroup);
        _warpGroup.Controls.Add(visual);
        _warpGroup.Controls.Add(save);
        _warpGroup.Controls.Add(cancel);

        Controls.Add(dungeonLabel);
        Controls.Add(_dungeon);
        Controls.Add(_warpGroup);
    }

    private void LoadValues()
    {
        SelectChoice(_dungeon, _command.DungeonId);
        SelectChoice(_map, _command.MapId);

        _x.Value = Math.Clamp(
            _command.X,
            (byte)0,
            (byte)Math.Min(255, Options.Instance.Map.MapWidth - 1)
        );
        _y.Value = Math.Clamp(
            _command.Y,
            (byte)0,
            (byte)Math.Min(255, Options.Instance.Map.MapHeight - 1)
        );

        _direction.SelectedIndex = Math.Clamp((int)_command.Direction, 0, _direction.Items.Count - 1);

        // Existing commands used only UsePartyInstance. Until one is edited and
        // saved, render its old behavior as an equivalent Warp configuration.
        var changeInstance = _command.UseWarpSettings
            ? _command.ChangeInstance
            : true;
        var instanceType = _command.UseWarpSettings
            ? _command.InstanceType
            : (_command.UsePartyInstance ? MapInstanceType.Shared : MapInstanceType.Personal);

        _changeInstance.Checked = changeInstance;
        _instanceGroup.Visible = changeInstance;
        _instanceType.SelectedIndex = Math.Clamp(
            (int)instanceType,
            0,
            _instanceType.Items.Count - 1
        );

        UpdateCoordinateLabels();
    }

    private void OpenVisualInterface()
    {
        if (_map.SelectedItem is not Choice selectedMap)
            return;

        using var selection = new FrmWarpSelection();
        selection.SelectTile(selectedMap.Id, (int)_x.Value, (int)_y.Value);
        selection.ShowDialog();

        if (!selection.GetResult())
            return;

        SelectChoice(_map, selection.GetMap());
        _x.Value = Math.Clamp(selection.GetX(), 0, Options.Instance.Map.MapWidth - 1);
        _y.Value = Math.Clamp(selection.GetY(), 0, Options.Instance.Map.MapHeight - 1);
        UpdateCoordinateLabels();
    }

    private void UpdateCoordinateLabels()
    {
        _xLabel.Text = Strings.Warping.x.ToString(_x.Value);
        _yLabel.Text = Strings.Warping.y.ToString(_y.Value);
    }

    private static void SelectChoice(DarkComboBox combo, Guid id)
    {
        for (var index = 0; index < combo.Items.Count; ++index)
        {
            if (combo.Items[index] is Choice choice && choice.Id == id)
            {
                combo.SelectedIndex = index;
                return;
            }
        }

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private void Save()
    {
        _command.DungeonId = (_dungeon.SelectedItem as Choice)?.Id ?? Guid.Empty;
        _command.MapId = (_map.SelectedItem as Choice)?.Id ?? Guid.Empty;
        _command.X = (byte)_x.Value;
        _command.Y = (byte)_y.Value;
        _command.Direction = (WarpDirection)Math.Max(0, _direction.SelectedIndex);
        _command.UseWarpSettings = true;
        _command.ChangeInstance = _changeInstance.Checked;
        _command.InstanceType = (MapInstanceType)Math.Max(0, _instanceType.SelectedIndex);

        // Keep the legacy flag synchronized for older tooling/data readers.
        _command.UsePartyInstance = _command.InstanceType == MapInstanceType.Shared;

        _eventEditor.FinishCommandEdit();
    }
}
