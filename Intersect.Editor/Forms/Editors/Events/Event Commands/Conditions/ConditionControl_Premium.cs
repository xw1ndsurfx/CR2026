using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands.Conditions;

public sealed class ConditionControl_Premium : UserControl
{
    private readonly ComboBox _mode = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 180,
    };

    private readonly NumericUpDown _remainingDays = new()
    {
        Minimum = 1,
        Maximum = 3650,
        Width = 90,
        Value = 1,
    };

    private readonly Label _daysLabel = new()
    {
        Text = "days",
        AutoSize = true,
        Margin = new Padding(4, 8, 3, 3),
    };

    public ConditionControl_Premium()
    {
        Dock = DockStyle.Fill;

        _mode.Items.Add(new ModeChoice(PremiumConditionMode.Active, "Premium is active"));
        _mode.Items.Add(new ModeChoice(PremiumConditionMode.Inactive, "Premium is inactive / expired"));
        _mode.Items.Add(new ModeChoice(PremiumConditionMode.RemainingDaysAtLeast, "Premium remaining at least"));
        _mode.SelectedIndex = 0;
        _mode.SelectedIndexChanged += (_, _) => UpdateVisibility();

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(2, 8, 2, 2),
        };

        panel.Controls.Add(new Label
        {
            Text = "Premium",
            AutoSize = true,
            Margin = new Padding(3, 8, 8, 3),
        });
        panel.Controls.Add(_mode);
        panel.Controls.Add(_remainingDays);
        panel.Controls.Add(_daysLabel);
        Controls.Add(panel);

        UpdateVisibility();
    }

    public void SetupFormValues(PremiumStatusCondition condition)
    {
        for (var i = 0; i < _mode.Items.Count; ++i)
        {
            if (_mode.Items[i] is ModeChoice choice && choice.Mode == condition.Mode)
            {
                _mode.SelectedIndex = i;
                break;
            }
        }

        _remainingDays.Value = Math.Clamp(condition.RemainingDays, 1, 3650);
        UpdateVisibility();
    }

    public void SaveFormValues(PremiumStatusCondition condition)
    {
        condition.Mode = _mode.SelectedItem is ModeChoice choice
            ? choice.Mode
            : PremiumConditionMode.Active;
        condition.RemainingDays = (int)_remainingDays.Value;
    }

    private void UpdateVisibility()
    {
        var showDays = _mode.SelectedItem is ModeChoice
        {
            Mode: PremiumConditionMode.RemainingDaysAtLeast
        };

        _remainingDays.Visible = showDays;
        _daysLabel.Visible = showDays;
    }

    private sealed record ModeChoice(PremiumConditionMode Mode, string Text)
    {
        public override string ToString() => Text;
    }
}
