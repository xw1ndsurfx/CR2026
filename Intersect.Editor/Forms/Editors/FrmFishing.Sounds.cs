using System.Media;
using System.Text.Json;
using Intersect.Framework.Core.Fishing;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmFishing
{
    private SoundPlayer? _soundPreview;
    private void StopPreview() { _soundPreview?.Stop(); _soundPreview?.Dispose(); _soundPreview = null; }
    private FishingSounds? SelectedSounds => _soundScope switch
    {
        0 => _config.Sounds, 1 => _zone?.Sounds, 2 => _tool?.Sounds, 3 => _catch?.Sounds, _ => null,
    };
    private static IEnumerable<string> SoundRoots() => new[]
        { Path.Combine(AppContext.BaseDirectory, "resources", "sounds"), Path.Combine("resources", "sounds") }
        .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase);
    private static IEnumerable<string> SoundFiles()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in SoundRoots().Where(Directory.Exists))
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".wav" or ".ogg"))
                files.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
        return files.Order(StringComparer.OrdinalIgnoreCase);
    }
    private void BuildSounds()
    {
        StopPreview();
        var table = Page(5);
        var scope = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        foreach (var text in new[] { "Global", "Selected zone", "Selected tool", "Selected catch" }) scope.Items.Add(T(text));
        scope.SelectedIndex = _soundScope;
        scope.SelectedIndexChanged += (_, _) => { if (_loading) return; _soundScope = scope.SelectedIndex; BuildSelectedPage(); };
        Row(table, "Sound scope", scope);
        if (_soundScope == 2) ToolSelector(table);
        if (_soundScope == 3 && _zone is { } zone)
        {
            var list = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            foreach (var entry in zone.Catches) list.Items.Add(new Named<FishingCatchDefinition>(entry, c => ItemName(c.ItemId)));
            list.SelectedItem = list.Items.OfType<Named<FishingCatchDefinition>>().FirstOrDefault(n => n.Value == _catch);
            list.SelectedIndexChanged += (_, _) => { if (_loading) return; _catch = (list.SelectedItem as Named<FishingCatchDefinition>)?.Value; BuildSelectedPage(); };
            Row(table, "Catch", list);
        }
        if (SelectedSounds is not { } sounds) { Note(table, "Select the zone, tool or catch for this scope."); return; }
        var events = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 500 };
        foreach (var key in FishingSoundCatalog.Keys.Distinct()) events.Items.Add(key);
        if (!events.Items.Contains(_soundKey)) _soundKey = FishingSoundCatalog.Keys.First();
        events.SelectedItem = _soundKey;
        events.SelectedIndexChanged += (_, _) => { if (_loading) return; _soundKey = events.SelectedItem?.ToString() ?? "cast"; BuildSelectedPage(); };
        Row(table, "Sound event", events);
        var effective = FishingSoundCatalog.Resolve(_config, _soundScope >= 1 ? _zone : null,
            _soundScope >= 2 ? _tool : null, _soundScope >= 3 ? _catch : null, _soundKey);
        sounds.Cues.TryGetValue(_soundKey, out var defined);
        var reserved = _soundKey is "challenge_timing_prompt" or "challenge_reel_prompt";
        var mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Enabled = !reserved };
        foreach (var text in new[] { "Inherit", "Silent", "Custom sound" }) mode.Items.Add(T(text));
        mode.SelectedIndex = defined is null ? 0 : string.IsNullOrEmpty(defined.File) ? 1 : 2;
        mode.SelectedIndexChanged += (_, _) => {
            if (_loading) return;
            if (mode.SelectedIndex == 0) sounds.Cues.Remove(_soundKey);
            else
            {
                var next = JsonSerializer.Deserialize<FishingSoundCue>(JsonSerializer.Serialize(effective))!;
                if (mode.SelectedIndex == 1) next.File = "";
                else if (next.File.Length == 0)
                {
                    next.File = FishingSoundCatalog.Default(_soundKey, _tool?.Type ?? FishingToolType.Rod).File;
                    if (next.File.Length == 0)
                    {
                        var proposed = "fishing/fishing_" + FishingSoundCatalog.Canonical(_soundKey, _tool?.Type ?? FishingToolType.Rod) + ".wav";
                        next.File = SoundFiles().Contains(proposed, StringComparer.OrdinalIgnoreCase) ? proposed : SoundFiles().FirstOrDefault() ?? "";
                    }
                }
                sounds.Cues[_soundKey] = next;
            }
            Changed(); BuildSelectedPage();
        };
        Row(table, "Override", mode);
        var cue = defined ?? effective;
        var custom = !reserved && mode.SelectedIndex == 2;
        var file = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 560, Enabled = custom };
        file.Items.Add(new FileChoice("", T("None")));
        foreach (var path in SoundFiles()) file.Items.Add(new FileChoice(path, path));
        if (!file.Items.OfType<FileChoice>().Any(c => c.Path.Equals(cue.File, StringComparison.OrdinalIgnoreCase)))
            file.Items.Add(new FileChoice(cue.File, string.Format(T("Missing sound: {0}"), cue.File)));
        file.SelectedItem = file.Items.OfType<FileChoice>().First(c => c.Path.Equals(cue.File, StringComparison.OrdinalIgnoreCase));
        file.SelectedIndexChanged += (_, _) => { if (!_loading && custom && file.SelectedItem is FileChoice choice) { cue.File = choice.Path; Changed(); } };
        Row(table, "Sound file", file);
        var controlsStart = table.Controls.Count;
        Number(table, "Volume (%)", cue.Volume, 0, 100, v => cue.Volume = (int)v);
        Number(table, "Cooldown (ms)", cue.CooldownMs, 0, 600000, v => cue.CooldownMs = (int)v);
        Number(table, "Maximum simultaneous sounds", cue.MaximumInstances, 1, 16, v => cue.MaximumInstances = (int)v);
        Check(table, "Loop", cue.Loop, v => cue.Loop = v);
        Check(table, "Spatial sound", cue.Spatial, v => cue.Spatial = v);
        Number(table, "Sound range (tiles)", cue.Range, 0, 100, v => cue.Range = (int)v);
        foreach (var control in table.Controls.Cast<Control>().Skip(controlsStart)) control.Enabled = custom;
        var preview = new FlowLayoutPanel { AutoSize = true };
        AddButton(preview, "Preview sound", () => {
            StopPreview();
            if (cue.File.Length == 0) { _status.Text = T("This event is silent."); return; }
            if (!cue.File.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) { _status.Text = T("Editor preview supports WAV files. Other formats must be checked in game."); return; }
            if (!FishingRules.SafeAssetPath(cue.File, ".wav")) return;
            var path = SoundRoots().Select(root => Path.Combine(root, cue.File.Replace('/', Path.DirectorySeparatorChar))).FirstOrDefault(File.Exists);
            if (path is null) { _status.Text = T("Sound is not available in the local resources."); return; }
            try { _soundPreview = new SoundPlayer(path); _soundPreview.Load(); if (cue.Loop) _soundPreview.PlayLooping(); else _soundPreview.Play(); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
            { StopPreview(); _status.Text = T("Sound could not be played."); }
        });
        AddButton(preview, "Stop preview", StopPreview); Row(table, "", preview);
        Note(table, "Preview uses system volume. The configured volume is applied in game.");
        if (reserved) Note(table, "Reserved: this challenge type is not implemented.");
        Note(table, "Inheritance: catch → tool → zone → global → default. Silent overrides prevent fallback.");
        Note(table, "Looped sounds must stop when their state or the fishing session ends.");
    }
}
