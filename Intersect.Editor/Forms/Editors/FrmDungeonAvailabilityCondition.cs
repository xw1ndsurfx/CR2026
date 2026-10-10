using DarkUI.Controls;
using DarkUI.Forms;
using Intersect.Editor.Forms.Editors.Events.Event_Commands.Conditions;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;

namespace Intersect.Editor.Forms.Editors;

/// <summary>
/// Uses the exact same phase, clock and calendar editor control as an Event
/// Conditional Branch; stores only the three supported public time rules.
/// </summary>
public sealed class FrmDungeonAvailabilityCondition : DarkForm
{
    private sealed record KindChoice(ConditionType Kind, string Caption)
    {
        public override string ToString() => Caption;
    }

    private readonly DarkComboBox _kind = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly ConditionControl_TimeAndDate _conditionEditor = new();
    private readonly DungeonAvailabilityCondition _original;

    public DungeonAvailabilityCondition? Result { get; private set; }

    public FrmDungeonAvailabilityCondition(DungeonAvailabilityCondition? original = null)
    {
        _original = original ?? new DungeonAvailabilityCondition();
        Text = "Dungeon - Time and Date Condition";
        Width = 485;
        Height = 490;
        MinimumSize = new Size(440, 460);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
        ForeColor = System.Drawing.Color.Gainsboro;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15, 12, 15, 10),
            RowCount = 3,
            ColumnCount = 1,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 83));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
        };
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Controls.Add(new Label
        {
            Text = "Condition type (same as Event Conditional Branch)",
            ForeColor = System.Drawing.Color.Gainsboro,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        _kind.Items.Add(new KindChoice(ConditionType.TimePhase, "Time Phase - Sunrise / Day / Sunset / Night"));
        _kind.Items.Add(new KindChoice(ConditionType.ClockTime, "Clock Time - exact / before / after / between"));
        _kind.Items.Add(new KindChoice(ConditionType.CalendarDate, "Calendar Date - on / before / after / range"));
        header.Controls.Add(_kind, 0, 1);

        var editorHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 8),
        };
        _conditionEditor.Dock = DockStyle.Fill;
        editorHost.Controls.Add(_conditionEditor);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 11, 0, 0),
        };
        var cancel = new DarkButton { Text = "Cancel", Width = 125, Height = 32 };
        var save = new DarkButton { Text = "Save Condition", Width = 145, Height = 32 };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        save.Click += (_, _) => SaveCondition();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        CancelButton = cancel;
        AcceptButton = save;

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(editorHost, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);

        foreach (KindChoice item in _kind.Items)
        {
            if (item.Kind != _original.Type)
                continue;

            _kind.SelectedItem = item;
            break;
        }
        if (_kind.SelectedIndex < 0)
            _kind.SelectedIndex = 0;

        _kind.SelectedIndexChanged += (_, _) => UpdateEditorKind();
        UpdateEditorKind();
        LoadExistingValues();
    }

    private void UpdateEditorKind()
    {
        var selected = (_kind.SelectedItem as KindChoice)?.Kind ?? ConditionType.TimePhase;
        _conditionEditor.ShowFor(selected);
    }

    private void LoadExistingValues()
    {
        switch (_original.Type)
        {
            case ConditionType.TimePhase:
                _conditionEditor.SetupFormValues(new TimePhaseCondition
                {
                    Phase = _original.Phase,
                });
                break;

            case ConditionType.ClockTime:
                _conditionEditor.SetupFormValues(new ClockTimeCondition
                {
                    Mode = _original.ClockMode,
                    StartMinute = _original.StartMinute,
                    EndMinute = _original.EndMinute,
                    UseRealUtcTime = _original.UseRealUtcTime,
                });
                break;

            case ConditionType.CalendarDate:
                _conditionEditor.SetupFormValues(new CalendarDateCondition
                {
                    Mode = _original.DateMode,
                    StartDate = _original.StartDate,
                    EndDate = _original.EndDate,
                    RepeatAnnually = _original.RepeatAnnually,
                });
                break;
        }
    }

    private void SaveCondition()
    {
        var type = (_kind.SelectedItem as KindChoice)?.Kind ?? ConditionType.TimePhase;
        var draft = new DungeonAvailabilityCondition { Type = type };

        switch (type)
        {
            case ConditionType.TimePhase:
                var phase = new TimePhaseCondition();
                _conditionEditor.SaveFormValues(phase);
                draft.Phase = phase.Phase;
                break;

            case ConditionType.ClockTime:
                var clock = new ClockTimeCondition();
                _conditionEditor.SaveFormValues(clock);
                draft.ClockMode = clock.Mode;
                draft.StartMinute = clock.StartMinute;
                draft.EndMinute = clock.EndMinute;
                draft.UseRealUtcTime = clock.UseRealUtcTime;
                break;

            case ConditionType.CalendarDate:
                var date = new CalendarDateCondition();
                _conditionEditor.SaveFormValues(date);
                draft.DateMode = date.Mode;
                draft.StartDate = date.StartDate;
                draft.EndDate = date.EndDate;
                draft.RepeatAnnually = date.RepeatAnnually;
                break;
        }

        if (!draft.IsStructurallyValid)
        {
            MessageBox.Show(
                this,
                "This time/date condition is invalid. Check the selected range and values.",
                "Dungeon Availability",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        Result = draft;
        DialogResult = DialogResult.OK;
        Close();
    }
}
