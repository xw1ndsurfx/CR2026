using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.Professions;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands.Conditions;

public sealed class ConditionControl_Profession : UserControl
{
    private sealed record Choice(Guid Id, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly ComboBox _profession = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 260,
    };

    private readonly ComboBox _comparator = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 145,
    };

    private readonly NumericUpDown _level = new()
    {
        Minimum = 0,
        Maximum = 500,
        Width = 80,
    };

    public ConditionControl_Profession()
    {
        Dock = DockStyle.Fill;

        _comparator.Items.AddRange(new object[]
        {
            VariableComparator.Equal,
            VariableComparator.GreaterOrEqual,
            VariableComparator.LesserOrEqual,
            VariableComparator.Greater,
            VariableComparator.Less,
            VariableComparator.NotEqual,
        });

        foreach (var profession in ProfessionConfiguration.Instance.Professions.OrderBy(x => x.Name))
            _profession.Items.Add(new Choice(profession.Id, profession.Name));

        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        panel.Controls.Add(new Label { Text = "Profession", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        panel.Controls.Add(_profession);
        panel.Controls.Add(new Label { Text = "Level", AutoSize = true, Margin = new Padding(8, 8, 3, 3) });
        panel.Controls.Add(_comparator);
        panel.Controls.Add(_level);
        Controls.Add(panel);
    }

    public void SetupFormValues(ProfessionLevelCondition condition)
    {
        for (var i = 0; i < _profession.Items.Count; ++i)
        {
            if ((_profession.Items[i] as Choice)?.Id == condition.ProfessionId)
            {
                _profession.SelectedIndex = i;
                break;
            }
        }

        if (_profession.SelectedIndex < 0 && _profession.Items.Count > 0) _profession.SelectedIndex = 0;
        _comparator.SelectedItem = condition.Comparator;
        if (_comparator.SelectedIndex < 0) _comparator.SelectedIndex = 1;
        _level.Value = Math.Clamp(condition.Value, 0, 500);
    }

    public void SaveFormValues(ProfessionLevelCondition condition)
    {
        condition.ProfessionId = (_profession.SelectedItem as Choice)?.Id ?? Guid.Empty;
        condition.Comparator = _comparator.SelectedItem is VariableComparator comparator
            ? comparator
            : VariableComparator.GreaterOrEqual;
        condition.Value = (int)_level.Value;
    }
}
