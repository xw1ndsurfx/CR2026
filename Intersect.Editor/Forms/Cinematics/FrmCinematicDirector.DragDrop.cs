using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.NPCs;

namespace Intersect.Editor.Forms.Cinematics;

internal static class CinematicDragData
{
    internal const string ActionFormat = "Logiklik.CinematicAction";
    internal const string NpcFormat = "Logiklik.CinematicNpc";
    internal static DataObject Action(CinematicTrack track) => new(ActionFormat, track.ToString());
    internal static DataObject Npc(Guid id) => new(NpcFormat, id.ToString());
    internal static bool TryAction(IDataObject? data, out CinematicTrack track) =>
        Enum.TryParse(data?.GetData(ActionFormat) as string, out track) && Enum.IsDefined(track);
    internal static bool TryNpc(IDataObject? data, out Guid id) =>
        Guid.TryParse(data?.GetData(NpcFormat) as string, out id) && id != Guid.Empty;
}

internal sealed partial class FrmCinematicDirector
{
    private sealed record PaletteAction(CinematicTrack Track)
    { public override string ToString() => T(Track.ToString()); }

    private void InitializeDragPalette(Panel sidebar)
    {
        var palette = new TabControl { Dock = DockStyle.Bottom, Height = 255 };
        var actionPage = new TabPage(T("ActionsPalette"));
        var npcPage = new TabPage(T("CharactersPalette"));
        var actions = new CinematicPicker<PaletteAction> { Dock = DockStyle.Fill, DragData = item => CinematicDragData.Action(item.Track) };
        actions.SetChoices(Enum.GetValues<CinematicTrack>().Select(track => new PaletteAction(track)));
        actions.Accepted += () => { if (actions.SelectedItem is { } item) AddClip(item.Track); };
        var npcs = new CinematicPicker<Resource> { Dock = DockStyle.Fill, DragData = item => CinematicDragData.Npc(item.Id) };
        void RefreshNpcs() => npcs.SetChoices(NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>()
            .Where(n => !string.IsNullOrWhiteSpace(n.Sprite)).OrderBy(n => n.Name).Select(n => new Resource(n.Id, n.Name)), npcs.SelectedItem);
        npcs.Accepted += () =>
        {
            if (npcs.SelectedItem is { } item) DropNpc(item.Id, new()
            { X = Intersect.Options.Instance.Map.MapWidth / 2, Y = Intersect.Options.Instance.Map.MapHeight / 2 });
        };
        actionPage.Controls.Add(actions); npcPage.Controls.Add(npcs);
        actionPage.Controls.Add(new Label { Text = T("DragActionsHint"), Dock = DockStyle.Top, Height = 40 });
        npcPage.Controls.Add(new Label { Text = T("DragCharactersHint"), Dock = DockStyle.Top, Height = 40 });
        palette.TabPages.Add(actionPage); palette.TabPages.Add(npcPage);
        palette.SelectedIndexChanged += (_, _) => { if (palette.SelectedTab == npcPage) RefreshNpcs(); };
        Shown += (_, _) => RefreshNpcs();
        sidebar.Controls.Add(palette);
        // Dock order leaves the scene tree above and the existing scene buttons below.
        sidebar.Controls.SetChildIndex(palette, 1);
        _timeline.ActionDropped += (track, time, actorId) =>
            AddClip(track, time, actorId is { } id ? _scene.Actors.FirstOrDefault(a => a.TargetId == id) : null);
        _viewport.ActionDropped += (track, position) => AddClip(track, position: position);
        _viewport.NpcDropped += DropNpc;
    }

    private void DropNpc(Guid npcId, CinematicPoint position)
    {
        if (NPCDescriptor.Get(npcId) is not { } npc || string.IsNullOrWhiteSpace(npc.Sprite)) return;
        if (_scene.Actors.Count >= 64) { _status.Text = T("scene-size"); return; }
        var actor = new CinematicActor { Name = npc.Name, TargetId = Guid.NewGuid(), Kind = CinematicActorKind.TemporaryNpc,
            NPCId = npc.Id, StartX = position.X, StartY = position.Y };
        _scene.Actors.Add(actor); SelectObject(actor); CommitEdit(); RenderMap();
    }
}
