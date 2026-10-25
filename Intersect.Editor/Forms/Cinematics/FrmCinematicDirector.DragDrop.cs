using System.Text.Json;
using Intersect.Editor.Content;
using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.NPCs;

namespace Intersect.Editor.Forms.Cinematics;

internal sealed record CinematicLibraryItem(CinematicTrack Track, Guid ResourceId, string Name, string AudioFile = "", bool Voice = false)
{ public override string ToString() => Name; }

internal static class CinematicDragData
{
    internal const string ActionFormat = "Logiklik.CinematicAction";
    internal const string NpcFormat = "Logiklik.CinematicNpc";
    internal const string ResourceFormat = "Logiklik.CinematicResource";
    internal static DataObject Action(CinematicTrack track) => new(ActionFormat, track.ToString());
    internal static DataObject Npc(Guid id) => new(NpcFormat, id.ToString());
    internal static DataObject Resource(CinematicLibraryItem item) => new(ResourceFormat, JsonSerializer.Serialize(item));
    internal static bool TryAction(IDataObject? data, out CinematicTrack track) =>
        Enum.TryParse(data?.GetData(ActionFormat) as string, out track) && Enum.IsDefined(track);
    internal static bool TryNpc(IDataObject? data, out Guid id) =>
        Guid.TryParse(data?.GetData(NpcFormat) as string, out id) && id != Guid.Empty;
    internal static bool TryResource(IDataObject? data, out CinematicLibraryItem? item)
    {
        item = null;
        if (data?.GetData(ResourceFormat) is not string json || json.Length > 8192) return false;
        try { item = JsonSerializer.Deserialize<CinematicLibraryItem>(json); }
        catch (JsonException) { return false; }
        return item is { Name.Length: > 0 } && (item.Track == CinematicTrack.Animation && item.ResourceId != Guid.Empty ||
            item.Track is CinematicTrack.Sound or CinematicTrack.Music && !string.IsNullOrWhiteSpace(item.AudioFile));
    }
}

internal sealed partial class FrmCinematicDirector
{
    private sealed record PaletteAction(CinematicTrack Track)
    { public override string ToString() => T(Track.ToString()); }

    private void InitializeDragPalette(Panel sidebar)
    {
        var scenePage = new TabPage(T("SceneLibrary"));
        foreach (var control in sidebar.Controls.Cast<Control>().ToArray()) scenePage.Controls.Add(control);
        var palette = new TabControl { Name = "MediaLibrary", Dock = DockStyle.Fill, Multiline = true };
        var npcPage = new TabPage(T("CharactersPalette"));
        var effectsPage = new TabPage(T("EffectsLibrary"));
        var audioPage = new TabPage(T("AudioLibrary"));
        var actionPage = new TabPage(T("ActionsPalette"));
        var actions = new CinematicPicker<PaletteAction> { Dock = DockStyle.Fill, DragData = item => CinematicDragData.Action(item.Track) };
        actions.SetChoices(Enum.GetValues<CinematicTrack>().Select(track => new PaletteAction(track)));
        actions.Accepted += () => { if (actions.SelectedItem is { } item) AddClip(item.Track); };
        var npcs = new CinematicPicker<Resource>(item => LoadLibraryThumbnail(NPCDescriptor.Get(item.Id)?.Sprite, "entities",
            Intersect.Options.Instance.Sprites.NormalFrames, Intersect.Options.Instance.Sprites.Directions), "♟")
            { Dock = DockStyle.Fill, DragData = item => CinematicDragData.Npc(item.Id) };
        var effects = new CinematicPicker<CinematicLibraryItem>(item =>
        {
            var animation = AnimationDescriptor.Get(item.ResourceId);
            var layer = !string.IsNullOrWhiteSpace(animation?.Lower.Sprite) ? animation.Lower : animation?.Upper;
            return LoadLibraryThumbnail(layer?.Sprite, "animations", layer?.XFrames ?? 1, layer?.YFrames ?? 1);
        }, "✦") { Dock = DockStyle.Fill, DragData = CinematicDragData.Resource };
        var audio = new CinematicPicker<CinematicLibraryItem>(glyph: "♪") { Dock = DockStyle.Fill, DragData = CinematicDragData.Resource };
        void RefreshLibrary()
        {
            npcs.SetChoices(NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>().Where(n => !string.IsNullOrWhiteSpace(n.Sprite))
                .OrderBy(n => n.Name).Select(n => new Resource(n.Id, n.Name)), npcs.SelectedItem);
            effects.SetChoices(AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(a => a.Name)
                .Select(a => new CinematicLibraryItem(CinematicTrack.Animation, a.Id, a.Name)), effects.SelectedItem);
            audio.SetChoices(GameContentManager.SmartSortedSoundNames.Select(n => new CinematicLibraryItem(CinematicTrack.Sound, Guid.Empty, T("Sound") + " · " + n, n))
                .Concat(GameContentManager.SmartSortedVoiceSoundNames.Select(n => new CinematicLibraryItem(CinematicTrack.Sound, Guid.Empty, T("VoiceSound") + " · " + n, n, true)))
                .Concat(GameContentManager.SmartSortedMusicNames.Select(n => new CinematicLibraryItem(CinematicTrack.Music, Guid.Empty, T("Music") + " · " + n, n))), audio.SelectedItem);
        }
        npcs.Accepted += () =>
        {
            if (npcs.SelectedItem is { } item) DropNpcOnTimeline(item.Id, _timeline.CursorMs);
        };
        effects.Accepted += () => { if (effects.SelectedItem is { } item) InsertLibraryItem(item, _timeline.CursorMs); };
        audio.Accepted += () => { if (audio.SelectedItem is { } item) InsertLibraryItem(item, _timeline.CursorMs); };
        npcPage.Controls.Add(npcs); effectsPage.Controls.Add(effects); audioPage.Controls.Add(audio); actionPage.Controls.Add(actions);
        palette.TabPages.AddRange([npcPage, effectsPage, audioPage, actionPage, scenePage]);
        palette.SelectedIndexChanged += (_, _) => RefreshLibrary();
        Shown += (_, _) => RefreshLibrary();
        sidebar.Controls.Add(palette);
        sidebar.Controls.Add(new Label { Text = T("MediaLibrary"), Dock = DockStyle.Top, Height = 30, Padding = new Padding(6) });
        sidebar.Controls.Add(new Label { Text = T("LibraryWorkflow"), Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(6) });
        _timeline.ActionDropped += (track, time, actorId) =>
            AddClip(track, time, actorId is { } id ? _scene.Actors.FirstOrDefault(a => a.TargetId == id) : null);
        _timeline.NpcDropped += DropNpcOnTimeline;
        _timeline.ResourceDropped += (item, time) => InsertLibraryItem(item, time);
        _viewport.ActionDropped += (track, position) => AddClip(track, position: position);
        _viewport.NpcDropped += (id, position) => DropNpcIntoScene(id, _timeline.CursorMs, position);
        _viewport.ResourceDropped += (item, position) => InsertLibraryItem(item, _timeline.CursorMs, position);
    }

    private static Bitmap? LoadLibraryThumbnail(string? name, string folder, int columns, int rows)
    {
        if (string.IsNullOrWhiteSpace(name) || EditorResourcePreview.ResolveFile(name, Path.Combine("resources", folder)) is not { } path) return null;
        using var sheet = EditorResourcePreview.LoadImage(path);
        if (sheet == null) return null;
        var width = Math.Max(1, sheet.Width / Math.Max(1, columns)); var height = Math.Max(1, sheet.Height / Math.Max(1, rows));
        var thumbnail = new Bitmap(56, 56);
        using var graphics = Graphics.FromImage(thumbnail);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        var scale = Math.Min(56f / width, 56f / height); var w = Math.Max(1, (int)(width * scale)); var h = Math.Max(1, (int)(height * scale));
        graphics.DrawImage(sheet, new Rectangle((56 - w) / 2, (56 - h) / 2, w, h), 0, 0, width, height, GraphicsUnit.Pixel);
        return thumbnail;
    }

    private void InsertLibraryItem(CinematicLibraryItem item, int time, CinematicPoint? position = null)
    {
        if (item.Track == CinematicTrack.Animation && AnimationDescriptor.Get(item.ResourceId) == null) return;
        AddClip(item.Track, time, position: position, resource: item);
        _timeline.CursorMs = _viewport.CursorMs = _timeline.Selected?.StartMs ?? time;
        RenderMap(); _status.Text = T(item.Track == CinematicTrack.Animation ? "PlaceOnMap" : "LibraryWorkflow");
    }

    private CinematicActor? DropNpc(Guid npcId, CinematicPoint position, bool visibleAtStart = true, bool commit = true)
    {
        if (NPCDescriptor.Get(npcId) is not { } npc || string.IsNullOrWhiteSpace(npc.Sprite)) return null;
        if (_scene.Actors.Count >= 64) { _status.Text = T("scene-size"); return null; }
        var actor = new CinematicActor { Name = npc.Name, TargetId = Guid.NewGuid(), Kind = CinematicActorKind.TemporaryNpc,
            NPCId = npc.Id, StartX = position.X, StartY = position.Y, VisibleAtStart = visibleAtStart };
        _scene.Actors.Add(actor);
        if (commit) { SelectObject(actor); CommitEdit(); RenderMap(); }
        return actor;
    }

    private void DropNpcOnTimeline(Guid npcId, int time)
    {
        DropNpcIntoScene(npcId, time, new() { X = Intersect.Options.Instance.Map.MapWidth / 2, Y = Intersect.Options.Instance.Map.MapHeight / 2 });
    }

    private void DropNpcIntoScene(Guid npcId, int time, CinematicPoint position)
    {
        if (DropNpc(npcId, position, false, false) is not { } actor) return;
        _timeline.CursorMs = _viewport.CursorMs = Math.Clamp(time, 0, Math.Max(0, _scene.DurationMs - 1000));
        AddClip(CinematicTrack.Actors, time, actor, resource: new(CinematicTrack.Actors, Guid.Empty, actor.Name));
        RenderMap();
        _status.Text = T("PlaceOnMap");
    }
}
