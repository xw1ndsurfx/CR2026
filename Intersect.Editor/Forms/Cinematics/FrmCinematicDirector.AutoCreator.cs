using Intersect.Editor.Content;
using Intersect.Editor.General;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.GameObjects.NPCs;
using DrawingPoint = System.Drawing.Point;

namespace Intersect.Editor.Forms.Cinematics;

internal sealed partial class FrmCinematicDirector
{
    private void OpenAutoCreator(bool fromDescription)
    {
        if (Globals.CurrentMap is not { } map) { _status.Text = T("MapRequired"); return; }
        _playing = false;
        var width = Intersect.Options.Instance.Map.MapWidth; var height = Intersect.Options.Instance.Map.MapHeight;
        var blocked = new bool[width, height];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) blocked[x, y] = map.Attributes[x, y] is MapBlockedAttribute;
        var context = new CinematicCreationContext
        {
            MapId = map.Id, MapName = map.Name, Width = width, Height = height, Blocked = (x, y) => blocked[x, y],
            Npcs = NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>().Where(n => !string.IsNullOrWhiteSpace(n.Sprite) && !string.IsNullOrWhiteSpace(n.Name))
                .OrderBy(n => n.Name).Select(n => new CreatorNpc(n.Id, n.Name)).ToArray(),
            Effects = AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().Where(a => !string.IsNullOrWhiteSpace(a.Name)).OrderBy(a => a.Name).Select(a => new CreatorEffect(a.Id, a.Name)).ToArray(),
            Music = GameContentManager.SmartSortedMusicNames, Sounds = GameContentManager.SmartSortedSoundNames,
        };
        try
        {
            _ = CinematicAutoCreator.Default(context);
            var image = Intersect.Editor.Core.Graphics.GetGraphicsDevice() == null ? null :
                Intersect.Editor.Core.Graphics.RenderCinematicMap(new() { MapId = map.Id }, 0);
            using var creator = new FrmCinematicAutoCreator(context, image, fromDescription);
            if (creator.ShowDialog(this) != DialogResult.OK || creator.Result == null || !CanDiscard()) return;
            LoadGeneratedScene(creator.Result);
        }
        catch (InvalidOperationException error) { _status.Text = error.Message; }
    }

    internal void LoadGeneratedScene(Intersect.Framework.Core.Cinematics.CinematicScene scene)
    {
        LoadScene(scene, Guid.Empty);
        _savedState = ""; CommitEdit(); _status.Text = T("CreatorOpenedUnsaved");
    }
}
