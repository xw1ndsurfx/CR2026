using Intersect.Editor.Forms.Cinematics;
using Intersect.Editor.General;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Maps;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;

internal sealed class EventCommandCinematic : UserControl
{
    private sealed record SceneChoice(Guid MapId, Guid EventId, string Text)
    { public override string ToString() => Text; }

    internal EventCommandCinematic(EventCommand command, FrmEvent editor)
    {
        Width = 600; Height = 420; Padding = new Padding(12);
        var simple = command is PlayCinematicCommand or StopCinematicCommand or WaitForCinematicCommand or StartCinematicChapterCommand;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42 };
        var apply = new Button { Text = CinematicText.T("Accept"), AutoSize = true, Enabled = !simple };
        Action beforeApply = () => { };
        apply.Click += (_, _) => { beforeApply(); editor.FinishCommandEdit(); };
        var cancel = new Button { Text = CinematicText.T("Cancel"), AutoSize = true };
        cancel.Click += (_, _) => editor.CancelCommandEdit();
        actions.Controls.Add(apply); actions.Controls.Add(cancel);

        if (simple)
        {
            var picker = new CinematicPicker<SceneChoice> { Dock = DockStyle.Fill };
            Controls.Add(picker);
            var chapter = command is StartCinematicChapterCommand;
            var help = new Label { Text = CinematicText.T(chapter ? "ChapterPickerHint" : "ScenePickerHint"),
                Dock = DockStyle.Top, Height = 56 };
            var title = new Label { Text = CinematicText.T(command.Type.ToString()), Dock = DockStyle.Top, Height = 32,
                Font = new Font(Font, FontStyle.Bold) };
            var selection = new Label { Dock = DockStyle.Bottom, Height = 44, AutoEllipsis = true };
            var choices = new List<SceneChoice>();
            var id = command switch
            {
                PlayCinematicCommand play => play.SceneEventId,
                StopCinematicCommand stop => stop.SceneEventId,
                WaitForCinematicCommand wait => wait.SceneEventId,
                StartCinematicChapterCommand start => start.ChapterEventId,
                _ => Guid.Empty,
            };
            if (command is StopCinematicCommand or WaitForCinematicCommand)
                choices.Add(new(Guid.Empty, Guid.Empty, CinematicText.T("AllCinematics")));
            if (chapter)
                choices.AddRange(EventDescriptor.Lookup.Values.OfType<EventDescriptor>()
                    .Where(e => e.CommonEvent && e.Pages.Count == 1 && e.Pages[0].CinematicChapter != null)
                    .OrderBy(e => e.Name).Select(e => new SceneChoice(Guid.Empty, e.Id, e.Name)));
            else
            {
                // Include the current editable map even before it has entered the shared cache.
                var maps = MapDescriptor.Lookup.Values.OfType<MapDescriptor>()
                    .Concat(new[] { Globals.CurrentMap, editor.MyMap }.OfType<MapDescriptor>())
                    .GroupBy(m => m.Id).Select(g => g.Last()).OrderBy(m => m.Name);
                foreach (var map in maps)
                foreach (var evt in map.LocalEvents.Values.Where(e => e.Pages.Count == 1 && e.Pages[0].Cinematic != null).OrderBy(e => e.Name))
                    choices.Add(new(map.Id, evt.Id, map.Name + " / " + evt.Name));
            }
            var current = choices.FirstOrDefault(c => c.EventId == id &&
                (command is not PlayCinematicCommand play || c.MapId == play.MapId));
            selection.Text = CinematicText.T(id != Guid.Empty && current == null ? "MissingCinematicChoice" : "ChooseCinematic");
            picker.SelectionChanged += selected =>
            {
                apply.Enabled = selected != null;
                if (selected == null) return;
                switch (command)
                {
                    case PlayCinematicCommand play: play.MapId = selected.MapId; play.SceneEventId = selected.EventId; break;
                    case StopCinematicCommand stop: stop.SceneEventId = selected.EventId; break;
                    case WaitForCinematicCommand wait: wait.SceneEventId = selected.EventId; break;
                    case StartCinematicChapterCommand start: start.ChapterEventId = selected.EventId; break;
                }
                selection.Text = selected.Text;
            };
            picker.SetChoices(choices, current);
            if (command is WaitForCinematicCommand waiting)
            {
                var row = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, WrapContents = false };
                row.Controls.Add(new Label { Text = CinematicText.T("WaitSeconds"), AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
                var seconds = new NumericUpDown { Minimum = .1m, Maximum = 600, DecimalPlaces = 3,
                    Value = Math.Clamp(waiting.TimeoutMs / 1000m, .1m, 600), Width = 110 };
                seconds.ValueChanged += (_, _) => waiting.TimeoutMs = (int)(seconds.Value * 1000);
                row.Controls.Add(seconds); Controls.Add(row);
                beforeApply = () => waiting.TimeoutMs = (int)(seconds.Value * 1000);
            }
            Controls.Add(selection); Controls.Add(help); Controls.Add(title);
        }
        else
        {
            var properties = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = new CinematicProperties(command), ToolbarVisible = false };
            Controls.Add(properties);
            if (command is CinematicEnvironmentCommand environment)
            {
                var settings = new Button { Text = CinematicText.T("EnvironmentSettings"), Dock = DockStyle.Top, Height = 32 };
                settings.Click += (_, _) => properties.SelectedObject = new CinematicProperties(environment.EnvironmentSettings ??= new());
                Controls.Add(settings);
            }
        }
        Controls.Add(actions);
        Intersect.Editor.Forms.Styling.EditorTheme.Apply(this);
    }
}
