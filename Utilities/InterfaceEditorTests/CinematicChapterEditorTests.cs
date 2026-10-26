using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Intersect.Editor.Forms.Cinematics;
using Intersect.Enums;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
using EventDescriptor = Intersect.Framework.Core.GameObjects.Events.EventDescriptor;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Models;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;

internal static class CinematicChapterEditorTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static IEnumerable<Control> All(Control root)
    { yield return root; foreach (Control child in root.Controls) foreach (var item in All(child)) yield return item; }
    private static void Capture(Control control, string name)
    {
        if (Environment.GetEnvironmentVariable("CODEX_UI_PREVIEW") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(folder, name + ".png"));
    }
    private static CinematicChapter Source(FrmCinematicChapters form) =>
        (CinematicChapter)typeof(FrmCinematicChapters).GetField("_chapter", Hidden)!.GetValue(form)!;
    private static GameObjectPacket Echo(IDatabaseObject item) => new(item.Id, item.Type, item.JsonData, false, false);
    private static GameObjectPacket Created(IDatabaseObject item) => new(item.Id, item.Type, item.JsonData, false, false) { CreationSucceeded = true };
    private static EventDescriptor Scene()
    {
        var id = Guid.NewGuid(); var map = Guid.NewGuid();
        return new(id, map, 2, 3) { Name = "Scène existante", Pages = [new EventPage { Cinematic = new() { Name = "Scène existante", MapId = map } }] };
    }
    internal static void Run()
    {
        var language = CultureInfo.CurrentUICulture;
        var scene = Scene(); EventDescriptor.Lookup.Set(scene.Id, scene);
        try
        {
            CultureInfo.CurrentUICulture = new("fr-FR");
            NewChapterNeedsNoManualVariable(scene);
            ExistingChapterKeepsProgressAndMetadata(scene);
            ConflictingTrackingCanBeRepairedByNamedChoice(scene);
            FailureNeverClaimsCompletion(scene);
            LateCreationCanBeRetriedWithoutReplacingProgress(scene);
            ResponsiveChapterLayout(scene);
            Locales();
            Console.WriteLine("PASS cinematic chapters: automatic personal completion, exact create/save receipts, unrelated and stale receipt isolation, preserved existing progression/page metadata, named choices, conflict repair, responsive chapter layout and en/fr/es fallback.");
        }
        finally { EventDescriptor.Lookup.DeleteAt(scene.Id); CultureInfo.CurrentUICulture = language; }
    }
    private static void AddScene(CinematicChapter chapter, EventDescriptor scene) =>
        chapter.Scenes.Add(new() { Name = scene.Name, MapId = scene.MapId, SceneEventId = scene.Id });

    private static void NewChapterNeedsNoManualVariable(EventDescriptor scene)
    {
        using var form = new FrmCinematicChapters();
        var source = Source(form); source.Name = "Arrivée"; AddScene(source, scene);
        Check(source.CompletionVariableId == Guid.Empty, "A new chapter starts without asking the author to select a variable.");
        var simple = new CinematicChapterProperties(source, source, Guid.Empty, false).GetProperties();
        Check(simple["CompletionVariableId"] == null && simple["UnlockVariableId"] == null, "Advanced progress IDs do not appear in ordinary chapter authoring.");
        Check(simple["StartQuestId"]!.Converter.ConvertToInvariantString(Guid.Empty) == CinematicText.T("ChapterNoQuest"), "Optional quests are named choices, not GUID text.");
        var creates = new List<(GameObjectType Type, Guid Id)>();
        var saves = new List<IDatabaseObject>();
        var session = form.PrepareSaveSession((type, id) => creates.Add((type, id)), saves.Add);
        Check(source.CompletionVariableId != Guid.Empty && source.UnlockVariableId == Guid.Empty, "Save prepares independent automatic completion without creating an unlock requirement.");
        Check(PlayerVariableDescriptor.Get(source.CompletionVariableId) == null && EventDescriptor.Get(session.EventId) == null,
            "Preparing a save never inserts optimistic objects into the editor registry.");
        session.Start();
        Check(creates.Count == 1 && creates[0] == (GameObjectType.PlayerVariable, source.CompletionVariableId) && saves.Count == 0,
            "Variable creation must precede its settings and the chapter.");
        session.Receive(Echo(new PlayerVariableDescriptor(Guid.NewGuid())));
        session.Receive(new(source.CompletionVariableId, GameObjectType.Event, "{}", false, false));
        Check(saves.Count == 0 && creates.Count == 1, "Unrelated and wrong-type packets cannot acknowledge our variable.");
        session.Receive(Echo(new PlayerVariableDescriptor(source.CompletionVariableId)));
        Check(saves.Count == 0, "An ordinary broadcast with the requested identity is not proof of successful creation.");
        session.Receive(Created(new PlayerVariableDescriptor(source.CompletionVariableId)));
        Check(saves.Count == 1 && saves[0] is PlayerVariableDescriptor { DataType: VariableDataType.Boolean } && creates.Count == 1,
            "Only the exact created variable receives the automatic settings; no chapter is sent yet.");
        var variable = (PlayerVariableDescriptor)saves[0];
        Check(variable.Name.Contains(source.Name) && variable.TextId.Length > 0, "Automatic tracking has an understandable name and stable text identity.");
        session.Receive(Echo(new PlayerVariableDescriptor(source.CompletionVariableId) { DataType = VariableDataType.Integer }));
        Check(creates.Count == 1, "A stale or incorrectly typed variable echo cannot release the dependent chapter save.");
        session.Receive(Echo(variable));
        Check(creates.Count == 2 && creates[1] == (GameObjectType.Event, session.EventId), "The chapter's common Event is created after the variable settings are persisted.");
        var placeholder = new EventDescriptor(session.EventId, true);
        session.Receive(Created(placeholder));
        Check(saves.Count == 2 && !session.Succeeded, "Sending chapter content does not claim it was saved.");
        var chapterEvent = (EventDescriptor)saves[1];
        var savedChapter = chapterEvent.Pages.Single().CinematicChapter!;
        Check(savedChapter.CompletionVariableId == variable.Id && savedChapter.UnlockVariableId == Guid.Empty, "The compiled chapter uses its own automatic progress flag.");
        var root = chapterEvent.Pages[0].CommandLists.Values.First();
        Check(root.OfType<ConditionalBranchCommand>().Count() == 1 && root.OfType<SetVariableCommand>().Single().SyncParty == false,
            "Native replay guards and personal authoritative completion are retained.");
        session.Receive(Echo(placeholder));
        Check(!session.Succeeded, "A delayed creation echo cannot be confused with persisted chapter content.");
        session.Receive(Echo(chapterEvent));
        Check(session.Succeeded && !session.Pending, "Only the exact saved chapter echo confirms success.");
        Check(EventDescriptor.Get(session.EventId) == null, "The coordinator never manufactures a saved registry state.");
    }

    private static void ExistingChapterKeepsProgressAndMetadata(EventDescriptor scene)
    {
        var progress = new PlayerVariableDescriptor(Guid.NewGuid()) { Name = "Progression existante" };
        PlayerVariableDescriptor.Lookup.Set(progress.Id, progress);
        var chapter = new CinematicChapter { Name = "Chapitre existant", CompletionVariableId = progress.Id, AllowReplay = true };
        AddScene(chapter, scene);
        var original = new EventDescriptor(Guid.NewGuid(), true) { Name = chapter.Name,
            Pages = [new EventPage { CinematicChapter = chapter, Description = "Preserved metadata", TriggerCommand = "/chapter-test" }] };
        EventDescriptor.Lookup.Set(original.Id, original);
        try
        {
            using var form = new FrmCinematicChapters(original);
            Source(form).Name = "Nouveau titre";
            var creates = new List<Guid>(); var saves = new List<IDatabaseObject>();
            var session = form.PrepareSaveSession((_, id) => creates.Add(id), saves.Add);
            session.Start();
            Check(creates.Count == 0 && saves.Count == 1 && saves[0].Id == original.Id, "Existing chapters retain their server identity without recreating resources.");
            var sent = (EventDescriptor)saves[0];
            Check(sent.Pages[0].CinematicChapter!.CompletionVariableId == progress.Id && sent.Pages[0].CinematicChapter!.AllowReplay,
                "An established completion flag and replay policy are never reset by the new workflow.");
            Check(sent.Pages[0].Description == "Preserved metadata" && sent.Pages[0].TriggerCommand == "/chapter-test", "Native Event metadata survives compilation.");
            Check(original.Name == "Chapitre existant" && original.Pages[0].CinematicChapter!.Name == "Chapitre existant",
                "The staged save cannot overwrite the known server snapshot before acknowledgement.");
            session.Receive(Echo(sent));
            Check(session.Succeeded, "Native Event JSON round-trips into a matching save receipt.");
        }
        finally { PlayerVariableDescriptor.Lookup.DeleteAt(progress.Id); EventDescriptor.Lookup.DeleteAt(original.Id); }
    }

    private static void ConflictingTrackingCanBeRepairedByNamedChoice(EventDescriptor scene)
    {
        var progress = new PlayerVariableDescriptor(Guid.NewGuid()) { Name = "Entrée débloquée" };
        PlayerVariableDescriptor.Lookup.Set(progress.Id, progress);
        try
        {
            using var form = new FrmCinematicChapters();
            var source = Source(form); AddScene(source, scene);
            source.UnlockVariableId = source.CompletionVariableId = progress.Id;
            var failed = false;
            try { form.PrepareSaveSession((_, _) => throw new Exception("Unexpected network create"), _ => throw new Exception("Unexpected save")); }
            catch (InvalidOperationException error) { failed = error.Message == "ChapterTrackingConflict"; }
            Check(failed, "A shared unlock/completion ID remains invalid; compiler protections are not weakened.");
            var properties = new CinematicChapterProperties(source, source, Guid.Empty, true).GetProperties();
            var completion = properties["CompletionVariableId"]!;
            Check(!completion.Converter.ConvertToInvariantString(progress.Id)!.Contains(progress.Id.ToString()),
                "Even invalid legacy selections do not expose GUIDs to the author.");
            Check(!completion.Converter.GetStandardValues()!.Cast<Guid>().Contains(progress.Id), "The unlock variable cannot be selected as completion.");
            completion.SetValue(source, Guid.Empty);
            var saves = new List<IDatabaseObject>();
            var fixedSession = form.PrepareSaveSession((_, _) => { }, saves.Add);
            Check(source.CompletionVariableId != Guid.Empty && source.CompletionVariableId != progress.Id && source.UnlockVariableId == progress.Id,
                "Choosing Automatic repairs tracking while preserving the authored unlock condition.");
            Check(fixedSession.CompletionId == source.CompletionVariableId && saves.Count == 0, "Repair still waits for native server creation.");
        }
        finally { PlayerVariableDescriptor.Lookup.DeleteAt(progress.Id); }
    }

    private static void FailureNeverClaimsCompletion(EventDescriptor scene)
    {
        using var form = new FrmCinematicChapters();
        var source = Source(form); AddScene(source, scene);
        var saves = new List<IDatabaseObject>();
        var session = form.PrepareSaveSession((_, _) => { }, saves.Add);
        session.Start();
        session.Receive(new(session.CompletionId, GameObjectType.PlayerVariable, "", false, false) { CreationError = "ChapterObjectConflict" });
        Check(!session.Pending && !session.Succeeded && saves.Count == 0 && session.StatusKey == "ChapterObjectConflict",
            "An exact creation conflict stops the workflow without overwriting any object.");
        session.Receive(Echo(new PlayerVariableDescriptor(session.CompletionId)));
        Check(saves.Count == 0, "A late acknowledgement cannot revive a failed save.");
        var savedSnapshot = (string)typeof(FrmCinematicChapters).GetField("_saved", Hidden)!.GetValue(form)!;
        Check(savedSnapshot != JsonConvert.SerializeObject(source), "Unconfirmed changes remain dirty.");

        var retry = form.PrepareSaveSession((_, _) => { }, saves.Add);
        retry.Start(); retry.Fail("ChapterSaveTimeout");
        Check(!retry.Succeeded && saves.Count == 0, "A timeout cannot send dependent chapter content or mark the draft saved.");
    }

    private static void LateCreationCanBeRetriedWithoutReplacingProgress(EventDescriptor scene)
    {
        using var form = new FrmCinematicChapters();
        _ = form.Handle;
        var source = Source(form); AddScene(source, scene);
        var creates = new List<(GameObjectType Type, Guid Id)>(); var saves = new List<IDatabaseObject>();
        var session = form.PrepareSaveSession((type, id) => creates.Add((type, id)), saves.Add);
        typeof(FrmCinematicChapters).GetField("_saveSession", Hidden)!.SetValue(form, session);
        session.Start(); session.Fail("ChapterSaveTimeout");
        var variable = new PlayerVariableDescriptor(session.CompletionId);
        var createdEvent = new EventDescriptor(session.EventId, true);
        try
        {
            PlayerVariableDescriptor.Lookup.Set(variable.Id, variable);
            typeof(FrmCinematicChapters).GetMethod("ReceiveObject", Hidden)!.Invoke(form, [Created(variable)]);
            Check(saves.Count == 0 && !session.Succeeded, "A late creation receipt remembers ownership without resuming an abandoned save.");
            var retry = form.PrepareSaveSession((type, id) => creates.Add((type, id)), saves.Add);
            typeof(FrmCinematicChapters).GetField("_saveSession", Hidden)!.SetValue(form, retry);
            retry.Start();
            Check(creates.Count == 1 && saves[0].Id == variable.Id, "Retry reuses the acknowledged automatic variable instead of creating another.");
            PlayerVariableDescriptor.Lookup.Set(variable.Id, saves[0]);
            retry.Receive(Echo(saves[0]));
            Check(creates.Count == 2 && creates[1] == (GameObjectType.Event, createdEvent.Id), "Retry keeps the original requested chapter identity.");
            retry.Fail("ChapterSaveTimeout");
            EventDescriptor.Lookup.Set(createdEvent.Id, createdEvent);
            typeof(FrmCinematicChapters).GetMethod("ReceiveObject", Hidden)!.Invoke(form, [Created(createdEvent)]);
            Check(saves.Count == 1 && !retry.Succeeded, "A late Event creation does not publish chapter content by itself.");
            var final = form.PrepareSaveSession((type, id) => creates.Add((type, id)), saves.Add);
            final.Start(); final.Receive(Echo(saves[^1]));
            Check(creates.Count == 2 && saves[^1].Id == createdEvent.Id && saves[^1] is EventDescriptor,
                "A second retry safely updates the already-created chapter; no orphan resources accumulate.");
            final.Receive(Echo(saves[^1]));
            Check(final.Succeeded, "The retried chapter still requires its saved content receipt.");
        }
        finally { PlayerVariableDescriptor.Lookup.DeleteAt(variable.Id); EventDescriptor.Lookup.DeleteAt(createdEvent.Id); }
    }

    private static void ResponsiveChapterLayout(EventDescriptor scene)
    {
        var map = new MapListMap { MapId = scene.MapId, Name = "Carte de démonstration" };
        MapList.OrderedMaps.Add(map);
        try
        {
            using var form = new FrmCinematicChapters
            { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
            form.Show(); Application.DoEvents();
            var source = Source(form); source.Name = "Chapitre de démonstration";
            var toolbar = All(form).OfType<FlowLayoutPanel>().Single(p => p.Dock == DockStyle.Top);
            var properties = All(form).OfType<PropertyGrid>().Single();
            var scenes = All(form).OfType<ListBox>().Single();
            var library = All(form).OfType<TreeView>().Single();
            var guide = (Label)typeof(FrmCinematicChapters).GetField("_guide", Hidden)!.GetValue(form)!;
            foreach (var size in new[] { new Size(1100, 760), new Size(800, 550) })
            {
                if (size.Width == 800)
                {
                    AddScene(source, scene);
                    typeof(FrmCinematicChapters).GetMethod("RefreshScenes", Hidden)!.Invoke(form, null);
                    scenes.SelectedIndex = 0;
                }
                form.Size = size; form.PerformLayout(); properties.Refresh(); Application.DoEvents();
                Check(toolbar.Visible && toolbar.Height >= 30, "The chapter toolbar remains visible at " + size);
                foreach (var key in new[] { "SaveChapter", "AddChapterScene", "ChapterProperties" })
                {
                    var button = All(form).OfType<Button>().Single(b => b.Name == key);
                    Check(button.Visible && button.Enabled && toolbar.ClientRectangle.Contains(button.Bounds),
                        "The full " + key + " button remains accessible at " + size);
                }
                foreach (var control in new Control[] { library, scenes, properties })
                {
                    var bounds = form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                    Check(form.ClientRectangle.Contains(bounds) && control.Height >= 180,
                        "Chapter panels stay inside the visible work area at " + size);
                }
                Check(library.Width >= 120 && scenes.Width >= 130 && properties.Width >= 200,
                    "Library, scene list and named settings retain usable widths at " + size);
                Check(scenes.PointToScreen(Point.Empty).Y >= toolbar.PointToScreen(Point.Empty).Y + toolbar.Height &&
                      scenes.PointToScreen(Point.Empty).Y >= guide.PointToScreen(Point.Empty).Y + guide.Height,
                    "The scene list starts below the toolbar and guidance instead of hiding either.");
                if (size.Width == 800)
                    Check(scenes.Items.Count == 1 && scenes.GetItemRectangle(0).Bottom <= scenes.ClientSize.Height,
                        "A selected scene remains readable at minimum window size.");
                Capture(form, "cinematic-chapters-" + size.Width);
            }
            form.Hide();
        }
        finally { MapList.OrderedMaps.Remove(map); }
    }

    private static void Locales()
    {
        using var stream = typeof(CinematicText).Assembly.GetManifestResourceStream("Cinematics.Texts.json")!;
        var catalog = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
        foreach (var entry in catalog.Where(p => p.Key.StartsWith("Chapter", StringComparison.Ordinal)))
        {
            string Tokens(string text) => string.Join(",", Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).Order());
            foreach (var language in new[] { "en", "fr", "es" })
                Check(entry.Value.TryGetValue(language, out var translated) && !string.IsNullOrWhiteSpace(translated) &&
                      Tokens(translated) == Tokens(entry.Value["en"]), "Chapter locale coverage and placeholder parity: " + entry.Key);
        }
        CultureInfo.CurrentUICulture = new("de-DE");
        Check(CinematicText.T("ChapterProgressHint") == catalog["ChapterProgressHint"]["en"], "Chapter hints retain English fallback.");
    }
}
