using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.WorldEvents.Invasions;
namespace Intersect.Editor.Forms.Editors.Events.Event_Commands.Conditions;

public sealed class ConditionControl_Invasion : UserControl
{
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly ComboBox _invasion = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly NumericUpDown _amount = new() { Minimum = 1, Maximum = 2000000000, Value = 1, Width = 140 };
    public ConditionControl_Invasion()
    {
        Dock = DockStyle.Fill;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        _invasion.Items.Add(new Choice(Guid.Empty, "Any / all invasions"));
        foreach (var invasion in InvasionConfiguration.Instance.Invasions.OrderBy(i => i.Name)) _invasion.Items.Add(new Choice(invasion.Id, invasion.Name));
        _invasion.SelectedIndex = 0;
        _mode.Items.AddRange(Enum.GetValues<InvasionConditionMode>().Cast<object>().ToArray()); _mode.SelectedIndex = 0;
        panel.Controls.AddRange([new Label { Text = "Invasion", AutoSize = true }, _invasion, new Label { Text = "Condition", AutoSize = true }, _mode, new Label { Text = "Minimum wave / count", AutoSize = true }, _amount]);
        _mode.SelectedIndexChanged += (_,_) => _amount.Enabled = (InvasionConditionMode)_mode.SelectedItem! >= InvasionConditionMode.WaveAtLeast;
        _amount.Enabled = false; Controls.Add(panel);
    }
    public void SetupFormValues(InvasionCondition condition)
    {
        if (!_invasion.Items.Cast<Choice>().Any(c => c.Id == condition.InvasionId)) _invasion.Items.Add(new Choice(condition.InvasionId, $"Configured invasion ({condition.InvasionId})"));
        _invasion.SelectedItem = _invasion.Items.Cast<Choice>().First(c => c.Id == condition.InvasionId);
        _mode.SelectedItem = condition.Mode; _amount.Value = Math.Clamp(condition.Amount, 1, 2000000000);
    }
    public void SaveFormValues(InvasionCondition condition)
    {
        condition.InvasionId = (_invasion.SelectedItem as Choice)?.Id ?? Guid.Empty;
        condition.Mode = (InvasionConditionMode)(_mode.SelectedItem ?? InvasionConditionMode.Active); condition.Amount = (int)_amount.Value;
    }
}
