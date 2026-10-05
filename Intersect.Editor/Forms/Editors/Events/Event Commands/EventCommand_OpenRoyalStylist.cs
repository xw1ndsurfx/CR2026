using DarkUI.Controls;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.RoyalStylist;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;

public sealed class EventCommand_OpenRoyalStylist : UserControl
{
    private sealed record StylistChoice(Guid Id, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly OpenRoyalStylistCommand _command;
    private readonly FrmEvent _editor;
    private readonly DarkComboBox _stylists = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 360,
    };

    public EventCommand_OpenRoyalStylist(
        OpenRoyalStylistCommand command,
        FrmEvent editor
    )
    {
        _command = command;
        _editor = editor;
        Width = 560;
        Height = 180;

        var group = new DarkGroupBox
        {
            Text = "Open Royal Stylist",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
        };

        var label = new Label
        {
            Text = "Royal Stylist",
            AutoSize = true,
            Location = new System.Drawing.Point(18, 42),
        };
        _stylists.Location = new System.Drawing.Point(120, 36);

        foreach (var stylist in RoyalStylistConfiguration.Instance.Stylists
                     .OrderBy(stylist => stylist.SortOrder)
                     .ThenBy(
                         stylist => stylist.Name,
                         StringComparer.OrdinalIgnoreCase
                     ))
        {
            _stylists.Items.Add(
                new StylistChoice(stylist.Id, stylist.Name)
            );
        }

        for (var i = 0; i < _stylists.Items.Count; i++)
        {
            if (_stylists.Items[i] is StylistChoice choice &&
                choice.Id == command.StylistId)
            {
                _stylists.SelectedIndex = i;
                break;
            }
        }

        if (_stylists.SelectedIndex < 0 &&
            _stylists.Items.Count > 0)
        {
            _stylists.SelectedIndex = 0;
        }

        var save = new DarkButton
        {
            Text = "Save",
            Width = 100,
            Height = 30,
            Location = new System.Drawing.Point(330, 105),
        };
        var cancel = new DarkButton
        {
            Text = "Cancel",
            Width = 100,
            Height = 30,
            Location = new System.Drawing.Point(438, 105),
        };

        save.Click += (_, _) =>
        {
            if (_stylists.SelectedItem is StylistChoice choice)
            {
                _command.StylistId = choice.Id;
            }

            _editor.FinishCommandEdit();
        };
        cancel.Click += (_, _) => _editor.CancelCommandEdit();

        group.Controls.Add(label);
        group.Controls.Add(_stylists);
        group.Controls.Add(save);
        group.Controls.Add(cancel);
        Controls.Add(group);
    }
}
