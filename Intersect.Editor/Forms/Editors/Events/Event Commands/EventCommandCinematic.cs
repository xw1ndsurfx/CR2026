using Intersect.Editor.Forms.Cinematics;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Maps;
namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;

internal sealed class EventCommandCinematic : UserControl
{
    private sealed record SceneChoice(Guid MapId, Guid EventId, string Text)
    { public override string ToString() => Text; }
    internal EventCommandCinematic(EventCommand command, FrmEvent editor)
    {
        Width = 600; Height = 360;
        var choices = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        var properties = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = command, ToolbarVisible = false };
        Controls.Add(properties);
        if (command is PlayCinematicCommand play)
        {
            foreach (var map in MapDescriptor.Lookup.Values.OfType<MapDescriptor>())
            foreach (var evt in map.LocalEvents.Values.Where(e => e.Pages.Count == 1 && e.Pages[0].Cinematic != null))
            {
                var choice = new SceneChoice(map.Id, evt.Id, map.Name + " / " + evt.Name); choices.Items.Add(choice);
                if (play.MapId == map.Id && play.SceneEventId == evt.Id) choices.SelectedItem = choice;
            }
            choices.SelectedIndexChanged += (_, _) =>
            { if (choices.SelectedItem is SceneChoice scene) { play.MapId = scene.MapId; play.SceneEventId = scene.EventId; properties.Refresh(); } };
            Controls.Add(choices);
        }
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36 };
        var apply = new Button { Text = CinematicText.T("Accept"), AutoSize = true };
        apply.Click += (_, _) => editor.FinishCommandEdit();
        var cancel = new Button { Text = CinematicText.T("Cancel"), AutoSize = true };
        cancel.Click += (_, _) => editor.CancelCommandEdit();
        actions.Controls.Add(apply); actions.Controls.Add(cancel); Controls.Add(actions);
    }
}
