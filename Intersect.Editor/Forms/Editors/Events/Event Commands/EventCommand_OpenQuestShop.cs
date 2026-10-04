using DarkUI.Controls;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.QuestShops;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;

public sealed class EventCommand_OpenQuestShop : UserControl
{
    private sealed record ShopChoice(Guid Id, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly OpenQuestShopCommand _command;
    private readonly FrmEvent _editor;
    private readonly DarkComboBox _shops = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 360,
    };

    public EventCommand_OpenQuestShop(OpenQuestShopCommand command, FrmEvent editor)
    {
        _command = command;
        _editor = editor;
        Width = 560;
        Height = 180;

        var group = new DarkGroupBox
        {
            Text = "Open Quest Shop",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
        };

        var label = new Label
        {
            Text = "Quest Shop",
            AutoSize = true,
            Location = new Point(18, 42),
        };
        _shops.Location = new Point(120, 36);

        foreach (var shop in QuestShopConfiguration.Instance.Shops
                     .OrderBy(shop => shop.SortOrder)
                     .ThenBy(shop => shop.Name, StringComparer.OrdinalIgnoreCase))
        {
            _shops.Items.Add(new ShopChoice(shop.Id, shop.Name));
        }

        for (var i = 0; i < _shops.Items.Count; i++)
        {
            if (_shops.Items[i] is ShopChoice choice && choice.Id == command.QuestShopId)
            {
                _shops.SelectedIndex = i;
                break;
            }
        }
        if (_shops.SelectedIndex < 0 && _shops.Items.Count > 0)
            _shops.SelectedIndex = 0;

        var save = new DarkButton { Text = "Save", Width = 100, Height = 30, Location = new Point(330, 105) };
        var cancel = new DarkButton { Text = "Cancel", Width = 100, Height = 30, Location = new Point(438, 105) };
        save.Click += (_, _) =>
        {
            if (_shops.SelectedItem is ShopChoice choice)
                _command.QuestShopId = choice.Id;
            _editor.FinishCommandEdit();
        };
        cancel.Click += (_, _) => _editor.CancelCommandEdit();

        group.Controls.Add(label);
        group.Controls.Add(_shops);
        group.Controls.Add(save);
        group.Controls.Add(cancel);
        Controls.Add(group);
    }
}
