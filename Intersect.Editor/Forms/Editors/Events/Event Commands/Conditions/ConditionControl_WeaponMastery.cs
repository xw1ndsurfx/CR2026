using Intersect.Editor.General;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands.Conditions;

public sealed class ConditionControl_WeaponMastery : UserControl
{
    private sealed record Choice(string Key, string Name) { public override string ToString() => Name; }
    private readonly ComboBox _mastery = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ComboBox _comparison = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 145 };
    private readonly NumericUpDown _level = new() { Minimum = 1, Maximum = 100, Width = 80 };
    private string _key = "";
    private bool _requested;
    private static string T(string text) => QualityOfLifeText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    public ConditionControl_WeaponMastery()
    {
        Dock = DockStyle.Fill;
        _comparison.Items.AddRange(new object[] { VariableComparator.Equal, VariableComparator.GreaterOrEqual, VariableComparator.LesserOrEqual, VariableComparator.Greater, VariableComparator.Less, VariableComparator.NotEqual });
        _comparison.SelectedItem = VariableComparator.GreaterOrEqual;
        _comparison.FormattingEnabled = true;
        _comparison.Format += (_, args) => args.Value = args.ListItem is VariableComparator value ? value switch
        {
            VariableComparator.Equal => "=", VariableComparator.GreaterOrEqual => ">=", VariableComparator.LesserOrEqual => "<=",
            VariableComparator.Greater => ">", VariableComparator.Less => "<", VariableComparator.NotEqual => "!=", _ => "?",
        } : args.Value;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        panel.Controls.Add(new Label { Text = T("Weapon mastery level"), AutoSize = true });
        panel.Controls.Add(_mastery); panel.Controls.Add(_comparison); panel.Controls.Add(_level); Controls.Add(panel);
        RoyalCombatEditorState.Changed += RefreshChoices;
        VisibleChanged += (_, _) =>
        {
            if (!Visible || _requested || Globals.MainForm == null) return;
            _requested = true;
            Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.GetConfiguration });
        };
        RefreshChoices();
    }
    private void RefreshChoices()
    {
        if (IsDisposed) return;
        var key = (_mastery.SelectedItem as Choice)?.Key ?? _key;
        _mastery.Items.Clear();
        foreach (var mastery in RoyalCombatEditorState.Current?.Masteries ?? []) _mastery.Items.Add(new Choice(mastery.Key, T(mastery.Name)));
        var selected = _mastery.Items.Cast<Choice>().FirstOrDefault(c => c.Key == key);
        if (selected == null && !string.IsNullOrEmpty(key))
        { selected = new(key, string.Format(T("Missing mastery: {0}"), key)); _mastery.Items.Add(selected); }
        _mastery.SelectedItem = selected ?? _mastery.Items.Cast<Choice>().FirstOrDefault();
    }
    public void SetupFormValues(WeaponMasteryLevelCondition condition)
    {
        _key = condition.MasteryKey ?? ""; _mastery.SelectedIndex = -1; RefreshChoices();
        _comparison.SelectedItem = condition.Comparator;
        _level.Value = Math.Clamp(condition.Value, 1, 100);
    }
    public void SaveFormValues(WeaponMasteryLevelCondition condition)
    {
        condition.MasteryKey = (_mastery.SelectedItem as Choice)?.Key ?? _key;
        condition.MasteryName = RoyalCombatEditorState.Current?.Masteries.FirstOrDefault(m => m.Key == condition.MasteryKey)?.Name ?? condition.MasteryName;
        condition.Comparator = _comparison.SelectedItem is VariableComparator comparator ? comparator : VariableComparator.GreaterOrEqual;
        condition.Value = (int)_level.Value;
    }
    protected override void Dispose(bool disposing) { if (disposing) RoyalCombatEditorState.Changed -= RefreshChoices; base.Dispose(disposing); }
}
