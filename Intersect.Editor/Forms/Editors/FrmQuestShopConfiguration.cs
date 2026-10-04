using DarkUI.Controls;
using DarkUI.Forms;
using Intersect.Editor.Networking;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.QuestShops;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmQuestShopConfiguration : DarkForm
{
    private sealed record QuestChoice(Guid Id, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly QuestShopConfiguration _working;
    private readonly List<QuestShopDefinition> _shops;
    private QuestShopDefinition? _selected;

    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly DarkTextBox _name = new() { Dock = DockStyle.Fill };
    private readonly DarkTextBox _description = new() { Dock = DockStyle.Fill, Multiline = true, Height = 90 };
    private readonly DarkNumericUpDown _sort = new() { Dock = DockStyle.Fill, Minimum = -100000, Maximum = 100000 };
    private readonly CheckedListBox _quests = new()
    {
        Dock = DockStyle.Fill,
        CheckOnClick = true,
        IntegralHeight = false,
    };

    public FrmQuestShopConfiguration()
    {
        Text = "Quest Shops";
        Width = 980;
        Height = 720;
        MinimumSize = new Size(820, 600);
        StartPosition = FormStartPosition.CenterScreen;

        _working = QuestShopConfiguration.FromJson(QuestShopConfiguration.Instance.ToJson());
        _shops = _working.Shops.ToList();

        foreach (var quest in QuestDescriptor.Lookup.Values
                     .OfType<QuestDescriptor>()
                     .OrderBy(quest => quest.Folder)
                     .ThenBy(quest => quest.Name, StringComparer.OrdinalIgnoreCase))
        {
            var label = string.IsNullOrWhiteSpace(quest.Folder)
                ? quest.Name
                : $"[{quest.Folder}] / {quest.Name}";
            _quests.Items.Add(new QuestChoice(quest.Id, label));
        }

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var cancel = new DarkButton { Text = "Cancel", Width = 105, Height = 30 };
        var save = new DarkButton { Text = "Save", Width = 105, Height = 30 };
        cancel.Click += (_, _) => Close();
        save.Click += (_, _) => Save();
        footer.Controls.Add(cancel);
        footer.Controls.Add(save);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildList(), 0, 0);
        root.Controls.Add(BuildEditor(), 1, 0);

        Controls.Add(root);
        Controls.Add(footer);

        _list.SelectedIndexChanged += (_, _) =>
        {
            CommitSelected();
            _selected = _list.SelectedItem as QuestShopDefinition;
            LoadSelected();
        };

        RefreshList();
    }

    private Control BuildList()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 10, 0) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42 };
        var add = new DarkButton { Text = "+ New", Width = 82, Height = 30 };
        var duplicate = new DarkButton { Text = "Duplicate", Width = 92, Height = 30 };
        var remove = new DarkButton { Text = "Delete", Width = 82, Height = 30 };

        add.Click += (_, _) =>
        {
            CommitSelected();
            var shop = new QuestShopDefinition();
            _shops.Add(shop);
            RefreshList(shop.Id);
        };
        duplicate.Click += (_, _) =>
        {
            if (_selected == null) return;
            CommitSelected();
            var copy = QuestShopConfiguration.FromJson(
                new QuestShopConfiguration { Shops = [_selected] }.ToJson()
            ).Shops[0];
            copy.Id = Guid.NewGuid();
            copy.Name += " Copy";
            _shops.Add(copy);
            RefreshList(copy.Id);
        };
        remove.Click += (_, _) =>
        {
            if (_selected == null) return;
            if (MessageBox.Show(this, $"Delete Quest Shop '{_selected.Name}'?", "Quest Shops",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _shops.RemoveAll(shop => shop.Id == _selected.Id);
            _selected = null;
            RefreshList();
        };

        buttons.Controls.Add(add);
        buttons.Controls.Add(duplicate);
        buttons.Controls.Add(remove);
        panel.Controls.Add(_list);
        panel.Controls.Add(buttons);
        return panel;
    }

    private Control BuildEditor()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, "Name", _name, 40);
        AddRow(table, "Description", _description, 100);
        AddRow(table, "Sort order", _sort, 40);
        AddRow(table, "Quests offered", _quests, 400);
        return table;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control, int height)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        table.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, row);
        control.Margin = new Padding(4);
        table.Controls.Add(control, 1, row);
    }

    private void RefreshList(Guid? selectId = null)
    {
        var id = selectId ?? _selected?.Id;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var shop in _shops.OrderBy(shop => shop.SortOrder).ThenBy(shop => shop.Name))
            _list.Items.Add(shop);
        _list.DisplayMember = nameof(QuestShopDefinition.Name);
        _list.EndUpdate();

        if (id.HasValue)
        {
            for (var i = 0; i < _list.Items.Count; i++)
            {
                if (_list.Items[i] is QuestShopDefinition shop && shop.Id == id)
                {
                    _list.SelectedIndex = i;
                    return;
                }
            }
        }

        _list.SelectedIndex = _list.Items.Count > 0 ? 0 : -1;
        if (_list.SelectedIndex < 0) LoadSelected();
    }

    private void LoadSelected()
    {
        var enabled = _selected != null;
        _name.Enabled = enabled;
        _description.Enabled = enabled;
        _sort.Enabled = enabled;
        _quests.Enabled = enabled;
        if (_selected == null) return;

        _name.Text = _selected.Name;
        _description.Text = _selected.Description;
        _sort.Value = Math.Clamp(_selected.SortOrder, -100000, 100000);
        for (var i = 0; i < _quests.Items.Count; i++)
        {
            var choice = (QuestChoice)_quests.Items[i]!;
            _quests.SetItemChecked(i, (_selected.QuestIds ?? []).Contains(choice.Id));
        }
    }

    private void CommitSelected()
    {
        if (_selected == null) return;
        _selected.Name = string.IsNullOrWhiteSpace(_name.Text) ? "Quest Shop" : _name.Text.Trim();
        _selected.Description = _description.Text?.Trim() ?? string.Empty;
        _selected.SortOrder = (int)_sort.Value;
        _selected.QuestIds = _quests.CheckedItems.Cast<QuestChoice>().Select(choice => choice.Id).Distinct().ToArray();
    }

    private void Save()
    {
        CommitSelected();
        _working.Shops = _shops.ToArray();
        if (!_working.IsStructurallyValid)
        {
            MessageBox.Show(this, "The Quest Shop configuration is invalid.", "Quest Shops",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        PacketSender.SendSaveQuestShopConfiguration(_working.ToJson());
        QuestShopConfiguration.Load(_working.ToJson());
        Close();
    }
}
