using DarkUI.Controls;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands.Conditions;

/// <summary>Shared time/date editing surface for event branches and spawn conditions.</summary>
public sealed class ConditionControl_TimeAndDate : UserControl
{
    private readonly Panel _phasePanel;
    private readonly Panel _clockPanel;
    private readonly Panel _datePanel;

    private readonly DarkComboBox _phase = NewCombo(Enum.GetValues<DayPhase>().Cast<object>().ToArray());
    private readonly DarkComboBox _clockMode = NewCombo(Enum.GetValues<ClockTimeMode>().Cast<object>().ToArray());
    private readonly DarkComboBox _dateMode = NewCombo(Enum.GetValues<CalendarDateMode>().Cast<object>().ToArray());

    private readonly DateTimePicker _startTime = NewClock();
    private readonly DateTimePicker _endTime = NewClock();
    private readonly DateTimePicker _startDate = NewDate();
    private readonly DateTimePicker _endDate = NewDate();

    private readonly CheckBox _utcClock = new()
    {
        Text = "Use real UTC clock (not game time)",
        ForeColor = System.Drawing.Color.Gainsboro,
        AutoSize = true,
        Margin = new Padding(3, 10, 3, 8),
    };
    private readonly CheckBox _annually = new()
    {
        Text = "Repeat every year",
        ForeColor = System.Drawing.Color.Gainsboro,
        AutoSize = true,
        Margin = new Padding(3, 10, 3, 8),
    };
    private readonly Label _clockEndLabel = TextLabel("End time (exclusive)");
    private readonly Label _dateEndLabel = TextLabel("End date (inclusive)");

    public ConditionControl_TimeAndDate()
    {
        Dock = DockStyle.Fill;
        BackColor = System.Drawing.Color.FromArgb(60, 63, 65);

        _phasePanel = Section(
            TextLabel("Game time phase"), _phase,
            Hint("Set phase boundaries in Time Editor. Night crosses midnight."));

        _clockPanel = Section(
            TextLabel("Clock condition"), _clockMode,
            TextLabel("Start / reference time"), _startTime,
            _clockEndLabel, _endTime, _utcClock,
            Hint("Ranges include the start, exclude the end, and may cross midnight."));

        _datePanel = Section(
            TextLabel("Calendar condition - real UTC date"), _dateMode,
            TextLabel("Start / reference date"), _startDate,
            _dateEndLabel, _endDate, _annually,
            Hint("Dates include both endpoints. Annual ranges may cross New Year."));

        Controls.Add(_phasePanel);
        Controls.Add(_clockPanel);
        Controls.Add(_datePanel);
        _clockMode.SelectedIndexChanged += (_, _) => UpdateVisibility();
        _dateMode.SelectedIndexChanged += (_, _) => UpdateVisibility();
        UpdateVisibility();
        ShowFor(ConditionType.TimePhase);
    }

    private static DarkComboBox NewCombo(object[] items)
    {
        var combo = new DarkComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 275,
            BackColor = System.Drawing.Color.FromArgb(69, 73, 74),
            ForeColor = System.Drawing.Color.Gainsboro,
            Margin = new Padding(4, 2, 3, 8),
        };
        combo.Items.AddRange(items);
        if (items.Length > 0)
            combo.SelectedIndex = 0;
        return combo;
    }

    private static DateTimePicker NewClock() => new()
    {
        Width = 155, Format = DateTimePickerFormat.Custom,
        CustomFormat = "HH:mm", ShowUpDown = true,
        Margin = new Padding(4, 2, 3, 8),
    };

    private static DateTimePicker NewDate() => new()
    {
        Width = 175, Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd",
        Margin = new Padding(4, 2, 3, 8),
    };

    private static Label TextLabel(string text) => new()
    {
        Text = text, AutoSize = true, ForeColor = System.Drawing.Color.Gainsboro,
        Margin = new Padding(4, 6, 3, 2),
    };

    private static Label Hint(string text) => new()
    {
        Text = text, AutoSize = true, MaximumSize = new Size(278, 0),
        ForeColor = System.Drawing.Color.Silver, Margin = new Padding(4, 14, 3, 3),
    };

    private static FlowLayoutPanel Section(params Control[] children)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            WrapContents = false, FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(7, 8, 4, 4),
            BackColor = System.Drawing.Color.FromArgb(60, 63, 65),
        };
        panel.Controls.AddRange(children);
        return panel;
    }

    private void UpdateVisibility()
    {
        var timeRange = _clockMode.SelectedItem is ClockTimeMode.Between;
        _clockEndLabel.Visible = timeRange;
        _endTime.Visible = timeRange;

        var dateMode = _dateMode.SelectedItem is CalendarDateMode mode
            ? mode : CalendarDateMode.Between;
        var dateRange = dateMode == CalendarDateMode.Between;
        _dateEndLabel.Visible = dateRange;
        _endDate.Visible = dateRange;
        _annually.Visible = dateMode is CalendarDateMode.On or CalendarDateMode.Between;
    }

    public void ShowFor(ConditionType type)
    {
        _phasePanel.Visible = type == ConditionType.TimePhase;
        _clockPanel.Visible = type == ConditionType.ClockTime;
        _datePanel.Visible = type == ConditionType.CalendarDate;
        base.Show();
    }

    private static int Minute(DateTimePicker input) =>
        input.Value.Hour * 60 + input.Value.Minute;

    private static void SetMinute(DateTimePicker input, int minute) =>
        input.Value = DateTime.Today.AddMinutes(Math.Clamp(minute, 0, 1439));

    public void SetupFormValues(TimePhaseCondition value) => _phase.SelectedItem = value.Phase;
    public void SaveFormValues(TimePhaseCondition value) =>
        value.Phase = _phase.SelectedItem is DayPhase phase ? phase : DayPhase.Night;

    public void SetupFormValues(ClockTimeCondition value)
    {
        _clockMode.SelectedItem = value.Mode;
        SetMinute(_startTime, value.StartMinute);
        SetMinute(_endTime, value.EndMinute);
        _utcClock.Checked = value.UseRealUtcTime;
        UpdateVisibility();
    }

    public void SaveFormValues(ClockTimeCondition value)
    {
        value.Mode = _clockMode.SelectedItem is ClockTimeMode mode ? mode : ClockTimeMode.Between;
        value.StartMinute = Minute(_startTime);
        value.EndMinute = Minute(_endTime);
        value.UseRealUtcTime = _utcClock.Checked;
    }

    public void SetupFormValues(CalendarDateCondition value)
    {
        _dateMode.SelectedItem = value.Mode;
        _startDate.Value = value.StartDate;
        _endDate.Value = value.EndDate;
        _annually.Checked = value.RepeatAnnually;
        UpdateVisibility();
    }

    public void SaveFormValues(CalendarDateCondition value)
    {
        value.Mode = _dateMode.SelectedItem is CalendarDateMode mode ? mode : CalendarDateMode.Between;
        value.StartDate = _startDate.Value.Date;
        value.EndDate = _endDate.Value.Date;
        value.RepeatAnnually = _annually.Checked &&
            (value.Mode is CalendarDateMode.On or CalendarDateMode.Between);
    }
}
