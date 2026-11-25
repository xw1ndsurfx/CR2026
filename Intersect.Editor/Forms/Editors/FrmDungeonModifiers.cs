using DarkUI.Forms;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.RoyalSeasons;
using static Intersect.Editor.Forms.Editors.RoyalSeasonEditorControls;
namespace Intersect.Editor.Forms.Editors;
internal sealed class FrmDungeonModifiers : DarkForm
{
    internal DungeonModifierConfiguration Draft { get; }
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly Panel _details = new() { Dock = DockStyle.Fill, AutoScroll = true };
    internal FrmDungeonModifiers(DungeonModifierConfiguration configuration)
    {
        Draft = RoyalSeasonRules.Read<DungeonModifierConfiguration>(RoyalSeasonRules.Json(configuration));
        Text = T("Dungeon modifiers"); Size = new Size(900, 680); MinimumSize = new Size(700, 500); StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Absolute, 155)); root.RowStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.Absolute, 50));
        var settings = Grid(); Check(settings, "Enable modifiers", Draft.Enabled, v => Draft.Enabled = v);
        var mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; mode.Items.AddRange([T("Fixed"), T("Weekly rotation (Monday UTC)")]); mode.SelectedIndex = (int)Draft.Mode;
        mode.SelectedIndexChanged += (_, _) => Draft.Mode = (DungeonModifierMode)mode.SelectedIndex; Row(settings, "Selection", mode);
        Number(settings, "Active modifiers in weekly rotation", Draft.ActiveCount, 1, 3, v => Draft.ActiveCount = v); root.Controls.Add(settings, 0, 0);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(850, 350), SplitterDistance = 240 };
        split.Panel1.Controls.Add(_list); split.Panel2.Controls.Add(_details);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 72 };
        actions.Controls.Add(ActionButton("Add modifier", () => { if (Draft.Pool.Length < 12) { Draft.Pool = [..Draft.Pool, new() { Name = T("New modifier"), EnemyHealthPercent = 125 }]; RefreshList(Draft.Pool.Length - 1); } }));
        actions.Controls.Add(ActionButton("Remove", () => { if (_list.SelectedIndex >= 0) { Draft.Pool = Draft.Pool.Where((_, i) => i != _list.SelectedIndex).ToArray(); RefreshList(0); } }));
        split.Panel1.Controls.Add(actions); _list.SelectedIndexChanged += (_, _) => ShowDetails(); root.Controls.Add(split, 0, 1);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill };
        footer.Controls.Add(ActionButton("Apply to dungeon", () => { if (!Draft.IsValid) { MessageBox.Show(T("Choose up to three fixed modifiers, or a weekly pool with enough entries."), Text); return; } DialogResult = DialogResult.OK; Close(); }));
        footer.Controls.Add(ActionButton("Cancel", Close)); root.Controls.Add(footer, 0, 2); Controls.Add(root); MiniGameEditorTheme.Apply(this); RefreshList(0);
    }
    private void RefreshList(int index) { _list.Items.Clear(); _list.Items.AddRange(Draft.Pool.Select(m => m.Name).Cast<object>().ToArray()); if (_list.Items.Count > 0) _list.SelectedIndex = Math.Clamp(index, 0, _list.Items.Count - 1); else ShowDetails(); }
    private void ShowDetails()
    {
        _details.Controls.Clear(); if (_list.SelectedIndex < 0) return;
        var value = Draft.Pool[_list.SelectedIndex]; var grid = Grid();
        RoyalSeasonEditorControls.Text(grid, "Name", value.Name, 80, v => value.Name = v);
        Number(grid, "Enemy health (%)", value.EnemyHealthPercent, 100, 500, v => value.EnemyHealthPercent = v);
        Number(grid, "Enemy damage (%)", value.EnemyDamagePercent, 100, 300, v => value.EnemyDamagePercent = v);
        Number(grid, "Remaining time (%)", value.TimeLimitPercent, 25, 100, v => value.TimeLimitPercent = v);
        Number(grid, "Lives removed (minimum one remains)", value.LivesPenalty, 0, 98, v => value.LivesPenalty = v);
        Number(grid, "Completion experience (%)", value.CompletionExperiencePercent, 100, 300, v => value.CompletionExperiencePercent = v);
        Row(grid, "Rules", new Label { AutoSize = true, MaximumSize = new Size(400, 0), Text = T("Modifiers are fixed at entry. Time reduction requires a dungeon time limit. Bonuses do not change item drops.") });
        _details.Controls.Add(grid); MiniGameEditorTheme.Apply(grid);
    }
}
