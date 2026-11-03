using Intersect.Editor.Maps;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Localization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Exports test content only. Installation is a separate, sandbox-guarded operation.
internal static class InteractiveScenario
{
    internal static int Export(string fixtureDirectory, string outputDirectory)
    {
        fixtureDirectory = Path.GetFullPath(fixtureDirectory);
        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        Intersect.Options.LoadFromServer(File.ReadAllText(Path.Combine(fixtureDirectory, "options.json")));
        var metadata = JObject.Parse(File.ReadAllText(Path.Combine(fixtureDirectory, "map.json")));
        var mapId = Guid.Parse((string)metadata["Id"]!);
        var map = new MapInstance(mapId);
        map.AttributeData = File.ReadAllBytes(Path.Combine(fixtureDirectory, "attributes.bin"));
        var catalog = new GameTextCatalog();
        string Text(string en, string fr, string es)
        { catalog.Entries[en] = new() { ["en"] = en, ["fr"] = fr, ["es"] = es }; return en; }
        var x = -1; var y = -1;
        // This fixture targets the land on the east side of Polonya Port.
        for (var py = 8; py < 18 && x < 0; py++)
        for (var px = 28; px < 31 && x < 0; px++)
            if (Enumerable.Range(py, 3).All(ty => map.Attributes[px, ty] is not MapBlockedAttribute)) { x = px; y = py; }
        if (x < 0) throw new InvalidOperationException("No valid test path on Polonya Port's eastern land.");
        Guid Id(int value) => new($"c19e0000-0000-4000-8000-{value:000000000000}");
        var gate = new PlayerVariableDescriptor(Id(1)) { Name = "QA cinematic interaction", TextId = "qa_cinematic_interaction" };
        var complete = new PlayerVariableDescriptor(Id(2)) { Name = "QA cinematic complete", TextId = "qa_cinematic_complete" };
        var selected = new PlayerVariableDescriptor(Id(3)) { Name = "QA cinematic choice", TextId = "qa_cinematic_choice" };
        SetVariableCommand Set(Guid id, bool value) => new() { VariableId = id, Modification = new BooleanVariableMod { Value = value } };
        List<EventCommand> Root(EventPage page) => page.CommandLists.Values.First();
        EventDescriptor Common(int id, string name) => new(Id(id), true) { Name = name, Pages = [new()] };
        var waiting = Common(10, "QA cinematic wait for interaction");
        var page = waiting.Pages[0];
        Root(page).Add(new LabelCommand { Label = "await-interaction" });
        Root(page).Add(new WaitCommand { Time = 250 });
        var condition = new ConditionalBranchCommand(page.CommandLists) { Condition = new VariableIsCondition
        { VariableId = gate.Id, Comparison = new BooleanVariableComparison { ComparingEqual = true, Value = true } } };
        Root(page).Add(condition);
        page.CommandLists[condition.BranchIds[1]].Add(new GoToLabelCommand { Label = "await-interaction" });
        var choiceA = Common(11, "QA cinematic choice A"); Root(choiceA.Pages[0]).Add(Set(selected.Id, false));
        var choiceB = Common(12, "QA cinematic choice B"); Root(choiceB.Pages[0]).Add(Set(selected.Id, true));
        var finish = Common(13, "QA cinematic record completion"); Root(finish.Pages[0]).Add(Set(complete.Id, true));
        var scene = new CinematicScene { MapId = mapId, Name = "QA interactive quay", Saga = "QA", Chapter = "Polonya Port", DurationMs = 8000 };
        scene.Actors.Add(new() { Name = Text("QA player", "Joueur de test", "Jugador de prueba"), StartX = x, StartY = y });
        scene.Clips.Add(new() { Track = CinematicTrack.Movement, Name = "QA native route", DurationMs = 1500,
            Points = [new() { X = x, Y = y }, new() { X = x, Y = y + 1 }] });
        scene.Clips.Add(new() { Track = CinematicTrack.Camera, X = x, Y = y + 1, Zoom = 1.5f, DurationMs = 1800 });
        scene.Clips.Add(new() { Track = CinematicTrack.Dialogue, StartMs = 2000, DurationMs = 1000,
            Text = Text("Local test: movement and camera finished. Confirm to continue.", "Test local : déplacement et caméra terminés. Confirmez pour continuer.", "Prueba local: movimiento y cámara terminados. Confirma para continuar.") });
        scene.Clips.Add(new() { Track = CinematicTrack.Choice, StartMs = 3000, DurationMs = 1000,
            Text = Text("Choose a test branch. Both continue to the required interaction.", "Choisissez une branche de test. Les deux mènent à l'interaction obligatoire.", "Elige una rama de prueba. Ambas llevan a la interacción obligatoria."),
            Option1 = Text("Branch A", "Branche A", "Rama A"), Option2 = Text("Branch B", "Branche B", "Rama B"),
            ChoiceEvent1 = choiceA.Id, ChoiceEvent2 = choiceB.Id });
        scene.Clips.Add(new() { Track = CinematicTrack.Dialogue, StartMs = 4000, DurationMs = 1000,
            Text = Text("Control will return. Step DOWN onto the test marker. The server must observe that action before the scene resumes.", "Vous reprenez le contrôle. Avancez vers le BAS sur le repère de test. Le serveur doit constater cette action avant la reprise.", "Recuperarás el control. Camina hacia ABAJO sobre la marca de prueba. El servidor debe observar la acción antes de continuar.") });
        scene.Clips.Add(new() { Track = CinematicTrack.Event, StartMs = 5000, DurationMs = 500, ResourceId = waiting.Id, AllowPlayerControl = true });
        scene.Clips.Add(new() { Track = CinematicTrack.Dialogue, StartMs = 5500, DurationMs = 1000,
            Text = Text("The server confirmed your interaction. The cinematic can now finish.", "Le serveur a confirmé votre interaction. La cinématique peut maintenant se terminer.", "El servidor confirmó tu interacción. La cinemática ya puede terminar.") });
        scene.Clips.Add(new() { Track = CinematicTrack.Event, StartMs = 6500, DurationMs = 500, ResourceId = finish.Id });
        var voice = Directory.Exists("resources/voicesound") ? Directory.GetFiles("resources/voicesound", "*.wav")
            .FirstOrDefault(file => string.Equals(Path.GetFileName(file), "Barkeeper_Chat_1.wav", StringComparison.OrdinalIgnoreCase)) : null;
        if (voice != null) scene.Clips.Add(new() { Track = CinematicTrack.Sound, AudioFile = Path.GetFileName(voice), VoiceSound = true, DurationMs = 1000 });
        var sceneEvent = new EventDescriptor(Id(20), mapId, 0, 0) { Name = scene.Name,
            Pages = [CinematicCompiler.Compile(scene, Intersect.Options.Instance.Map.MapWidth, Intersect.Options.Instance.Map.MapHeight,
                (px, py) => map.Attributes[px, py] is MapBlockedAttribute)] };
        var marker = new EventDescriptor(Id(21), mapId, x, y + 2) { Name = Text("QA interaction marker", "Repère d'interaction de test", "Marca de interacción de prueba"), CanRunInParallel = true };
        marker.Pages[0].Trigger = EventTrigger.PlayerCollide; marker.Pages[0].Passable = true;
        var existing = JArray.Parse(File.ReadAllText(Path.Combine(fixtureDirectory, "events.json"))).FirstOrDefault();
        if (existing != null)
        {
            var descriptor = new EventDescriptor(Guid.Parse((string)existing["Id"]!), existing.ToString());
            if (descriptor.Pages.Count > 0) marker.Pages[0].Graphic.CopyFrom(descriptor.Pages[0].Graphic);
        }
        Root(marker.Pages[0]).Add(Set(gate.Id, true));
        var launch = Common(22, "QA cinematic launch"); launch.Pages[0].CommonTrigger = CommonEventTrigger.SlashCommand; launch.Pages[0].TriggerCommand = "cinematicqa";
        Root(launch.Pages[0]).Add(Set(gate.Id, false)); Root(launch.Pages[0]).Add(Set(complete.Id, false));
        Root(launch.Pages[0]).Add(new WarpCommand { MapId = mapId, X = (byte)x, Y = (byte)y, Direction = WarpDirection.Down });
        Root(launch.Pages[0]).Add(new WaitCommand { Time = 1000 });
        Root(launch.Pages[0]).Add(new PlayCinematicCommand { MapId = mapId, SceneEventId = sceneEvent.Id });
        var events = new[] { waiting, choiceA, choiceB, finish, sceneEvent, marker, launch };
        foreach (var evt in events) evt.Pages = evt.Pages; // Refresh native cached PagesJson after editing command lists.
        File.WriteAllText(Path.Combine(outputDirectory, "scenario.json"), JsonConvert.SerializeObject(new
        {
            MapId = mapId, StartX = x, StartY = y, SceneEventId = sceneEvent.Id,
            Variables = new[] { gate, complete, selected }.Select(v => new { v.Id, v.Name, v.TextId, v.DataType, v.TimeCreated }),
            Events = events.Select(e => new { e.Id, e.Name, e.MapId, e.SpawnX, e.SpawnY, e.CommonEvent, e.Global, e.CanRunInParallel, e.TimeCreated, Pages = e.PagesJson }),
        }, Formatting.Indented));
        File.WriteAllText(Path.Combine(outputDirectory, "content.json"), catalog.ToJson());
        File.WriteAllText(Path.Combine(outputDirectory, "scene.cinematic.json"), JsonConvert.SerializeObject(scene, Formatting.Indented));
        Console.WriteLine($"Prepared interactive scenario on {mapId} at {x},{y}; marker {x},{y + 2}; /cinematicqa. Not installed in any database.");
        return 0;
    }
}
