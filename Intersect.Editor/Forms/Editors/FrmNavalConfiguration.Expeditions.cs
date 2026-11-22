using Intersect.Framework.Core.Naval;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmNavalConfiguration
{
    private Control ExpeditionStages(NavalExpeditionDefinition definition)
    {
        var group = new GroupBox { Text = T("Expedition stages"), Height = 620, Dock = DockStyle.Top, Padding = new(6, 24, 6, 6) };
        var list = new ListBox { Name = "ExpeditionStageList", Height = 110, Dock = DockStyle.Top, FormattingEnabled = true };
        var detail = new Panel { Dock = DockStyle.Fill };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, WrapContents = true };
        var add = new Button { Text = T("Add stage"), AutoSize = true };
        var remove = new Button { Text = T("Remove"), AutoSize = true };
        var up = new Button { Text = T("Move up"), AutoSize = true };
        var down = new Button { Text = T("Move down"), AutoSize = true };
        list.Format += (_, e) => e.Value = e.ListItem is NavalExpeditionStage stage
            ? $"{Array.IndexOf(definition.Stages, stage) + 1}. {T(stage.Objective.ToString())}" : "";
        void Populate(NavalExpeditionStage? selected)
        {
            list.Items.Clear(); list.Items.AddRange(definition.Stages); list.SelectedItem = selected;
            add.Enabled = definition.Stages.Length < 16;
        }
        list.SelectedIndexChanged += (_, _) =>
        {
            foreach (Control child in detail.Controls.Cast<Control>().ToArray()) child.Dispose();
            remove.Enabled = list.SelectedItem != null; up.Enabled = list.SelectedIndex > 0;
            down.Enabled = list.SelectedIndex >= 0 && list.SelectedIndex < list.Items.Count - 1;
            if (list.SelectedItem is not NavalExpeditionStage stage) return;
            var fields = Fields(stage, 0, stage.Objective is NavalExpeditionObjective.ReachPoint or NavalExpeditionObjective.ReturnToPort
                ? ["Id", "Objective", "TargetId", "Quantity"] : ["Id", "Objective", "TargetId", "X", "Y", "Radius"]);
            var objective = new ComboBox { Name = "ExpeditionObjective", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            objective.Items.AddRange(Enum.GetValues<NavalExpeditionObjective>().Select(k => T(k.ToString())).ToArray());
            objective.SelectedIndex = (int)stage.Objective;
            objective.SelectedIndexChanged += (_, _) =>
            { stage.Objective = (NavalExpeditionObjective)objective.SelectedIndex; stage.TargetId = Guid.Empty; Changed(0); Populate(stage); };
            var row = fields.RowCount++; fields.RowStyles.Add(new(SizeType.Absolute, 35));
            fields.Controls.Add(new Label { Text = T("Objective"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            fields.Controls.Add(objective, 1, row);
            if (stage.Objective is NavalExpeditionObjective.DefeatPatrol or NavalExpeditionObjective.DefeatMonster)
            {
                var target = new ComboBox { Name = "ExpeditionTarget", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
                void RefreshTargets()
                {
                    var choices = new List<Choice> { new(Guid.Empty, T("Automatic / not selected")) };
                    choices.AddRange(stage.Objective == NavalExpeditionObjective.DefeatPatrol
                        ? _navigation.Herbonia.Where(p => p.Ship.MapId == stage.MapId).Select(p => new Choice(p.Ship.Id, p.Ship.Name))
                        : _navigation.Monsters.Where(m => m.MapId == stage.MapId).Select(m => new Choice(m.Id, m.Name)));
                    target.Items.Clear(); target.Items.AddRange(choices.ToArray());
                    target.SelectedItem = choices.FirstOrDefault(c => c.Id == stage.TargetId) ?? choices[0];
                }
                RefreshTargets();
                target.SelectedIndexChanged += (_, _) => { if (target.SelectedItem is Choice choice) { stage.TargetId = choice.Id; Changed(0); } };
                if (fields.Controls.Find("MapId", true).FirstOrDefault() is ComboBox map)
                    map.SelectedIndexChanged += (_, _) => { stage.TargetId = Guid.Empty; RefreshTargets(); };
                row = fields.RowCount++; fields.RowStyles.Add(new(SizeType.Absolute, 35));
                fields.Controls.Add(new Label { Text = T("Naval target"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
                fields.Controls.Add(target, 1, row);
            }
            detail.Controls.Add(ScrollFields(fields)); MiniGameEditorTheme.Apply(detail);
        };
        add.Click += (_, _) =>
        {
            if (definition.Stages.Length >= 16) return;
            var stage = new NavalExpeditionStage { MapId = definition.Stages.LastOrDefault()?.MapId ?? _navigation.Herbonia.FirstOrDefault()?.Ship.MapId ?? _navigation.Monsters.FirstOrDefault()?.MapId ?? _navigation.Ships.FirstOrDefault()?.MapId ?? Guid.Empty };
            definition.Stages = [..definition.Stages, stage]; Changed(0); Populate(stage);
        };
        remove.Click += (_, _) => { if (list.SelectedItem is NavalExpeditionStage stage) { definition.Stages = definition.Stages.Where(s => s != stage).ToArray(); Changed(0); Populate(definition.Stages.FirstOrDefault()); } };
        void Move(int delta)
        {
            var index = list.SelectedIndex; var next = index + delta;
            if (index < 0 || next < 0 || next >= definition.Stages.Length) return;
            var selected = definition.Stages[index];
            (definition.Stages[index], definition.Stages[next]) = (definition.Stages[next], definition.Stages[index]); Changed(0); Populate(selected);
        }
        up.Click += (_, _) => Move(-1); down.Click += (_, _) => Move(1);
        buttons.Controls.AddRange([add, remove, up, down]);
        group.Controls.Add(detail); group.Controls.Add(list); group.Controls.Add(buttons);
        Populate(definition.Stages.FirstOrDefault()); return group;
    }
}
