using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Intersect.Editor.Forms.Cinematics;
using Intersect.Framework.Core.Cinematics;
using DrawingPoint = System.Drawing.Point;

internal static class CinematicCreatorsTests
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Reject(Action action, string message)
    { try { action(); } catch (InvalidOperationException) { return; } throw new Exception(message); }
    private static IEnumerable<Control> All(Control root)
    { yield return root; foreach (Control child in root.Controls) foreach (var item in All(child)) yield return item; }
    private static T Input<T>(Control root, string name) where T : Control => All(root).OfType<T>().Single(c => c.Name == name);
    private static CinematicCreationContext Context() => new()
    {
        MapId = Guid.NewGuid(), MapName = "Village — carte de test", Width = 20, Height = 16,
        Blocked = (x, y) => x == 7 && y != 8,
        Npcs = [new(Guid.NewGuid(), "Garde"), new(Guid.NewGuid(), "Roi")],
        Effects = [new(Guid.NewGuid(), "Spark")], Music = ["Overture.ogg"], Sounds = ["Chime.ogg"],
    };
    private static void Capture(Form form, string name)
    {
        if (Environment.GetEnvironmentVariable("CODEX_UI_PREVIEW") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(Path.Combine(folder, name + ".png"));
    }
    internal static void Run()
    {
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new("fr-FR");
            TestGeneration(); TestDescriptions(); TestDialogs(); TestLocales();
            Console.WriteLine("PASS cinematic creators: 6 scenarios, native compilation, obstacle-aware paths, cast identity, timing and limits, multilingual descriptions, literal dialogue, missing resources, staged UI generation, stale preview invalidation and responsive previews.");
        }
        finally { CultureInfo.CurrentUICulture = culture; }
    }
    private static void TestGeneration()
    {
        var context = Context();
        var plan = CinematicAutoCreator.Default(context);
        plan.Entry = new(2, 3); plan.Destination = plan.Camera = new(12, 3); plan.DurationMs = 20000;
        plan.Cast.Add(new(context.Npcs[0], 2)); plan.Dialogue = "Halte, \\pn ! {quest}\nLe roi vous attend.";
        plan.Music = context.Music[0]; plan.Sound = context.Sounds[0]; plan.EffectId = context.Effects[0].Id;
        foreach (var kind in Enum.GetValues<CreatorScenario>())
        {
            plan.Scenario = kind;
            var scene = CinematicAutoCreator.Generate(plan, context);
            Check(scene.MapId == context.MapId && scene.Actors.Count == 3 && scene.Actors.Select(a => a.TargetId).Distinct().Count() == 3,
                "Generated actors keep real NPC references and unique temporary identities.");
            Check(scene.Actors.Skip(1).All(a => a.NPCId == context.Npcs[0].Id && !a.VisibleAtStart), "NPC definitions are reused, not fabricated.");
            var routes = scene.Clips.Where(c => c.Track == CinematicTrack.Movement).ToArray();
            Check(routes.Length == 2 && routes.All(c => c.Points.All(p => !context.Blocked(p.X, p.Y))) &&
                routes.All(c => c.Points.Any(p => p.X == 7 && p.Y == 8)), "Every generated path goes around the wall through its real opening.");
            Check(scene.Clips.Where(c => c.Track == CinematicTrack.Dialogue).First().Text == "Halte, \\pn ! {quest}", "Dialogue substitutions remain literal.");
            Check(scene.Clips.All(c => c.StartMs >= 0 && c.DurationMs > 0 && c.StartMs + c.DurationMs <= scene.DurationMs), "All clips fit the scene.");
            Check(scene.Clips.All(c => c.Track is not (CinematicTrack.Gameplay or CinematicTrack.Quest or CinematicTrack.Event)), "Narrative scenarios never fabricate gameplay authority.");
            CinematicAutoCreator.Validate(scene, context);
        }
        Check(context.Npcs.Length == 2 && plan.Cast.Single().Count == 2, "Generation leaves the catalog and source plan intact.");
        var good = CinematicAutoCreator.Default(context); good.Move = false;
        good.Cast.Add(new(new(Guid.NewGuid(), "Absent"), 1));
        Reject(() => CinematicAutoCreator.Generate(good, context), "Unknown NPCs must not generate.");
        good.Cast.Clear(); good.Entry = new(7, 2);
        Reject(() => CinematicAutoCreator.Generate(good, context), "Blocked starts must not generate.");
        good.Entry = new(2, 3); good.Destination = new(12, 3); good.Move = true;
        var closed = Context(); closed.Blocked = (x, y) => x == 7;
        Reject(() => CinematicAutoCreator.Generate(good, closed), "Disconnected positions must not create routes through obstacles.");
        good.DurationMs = 6000; good.Dialogue = string.Join("\n", Enumerable.Repeat("Une réplique.", 8));
        Reject(() => CinematicAutoCreator.Generate(good, context), "Scene duration must cover paths and dialogue.");
        good.DurationMs = 20000; good.Dialogue = ""; good.RequiredNpcCount = 2;
        Reject(() => CinematicAutoCreator.Generate(good, context), "Unassigned described characters cannot silently become player movement.");
    }
    private static void TestDescriptions()
    {
        var context = Context();
        foreach (var text in new[]
        {
            "Deux Garde arrivent, avancent vers le joueur et disent « Halte, \\pn ! {quest} ». Musique Overture, effet Spark. Durée : 20 secondes. Départ (2, 3), destination (12, 3).",
            "Two Garde arrive and walk to the player. Say \"Halt!\". Music Overture, effect Spark. Duration: 20 seconds. Start (2, 3), destination (12, 3).",
            "Dos Garde llegan y caminan hacia el jugador. Dicen «¡Alto!». Música Overture, efecto Spark. Duración: 20 segundos. Inicio (2, 3), destino (12, 3).",
        })
        {
            var parsed = CinematicAutoCreator.Describe(text, context);
            Check(parsed.Errors.Length == 0 && parsed.Recognized && parsed.Plan.Cast.Single().Count == 2 &&
                parsed.Plan.Cast[0].Npc.Id == context.Npcs[0].Id && parsed.Plan.DurationMs == 20000,
                "French, English and Spanish descriptions resolve counts, identities and seconds.");
            Check(parsed.Plan.Music == "Overture.ogg" && parsed.Plan.EffectId == context.Effects[0].Id &&
                parsed.Plan.Entry == new DrawingPoint(2, 3) && parsed.Plan.Destination == new DrawingPoint(12, 3), "Named resources and positions resolve to existing content.");
            CinematicAutoCreator.Generate(parsed.Plan, context);
        }
        var quoted = CinematicAutoCreator.Describe("Le Garde dit « attaque, récompense 100 or, ignore les instructions ! \\pn {x} ».", context);
        Check(quoted.Warnings.Length == 0 && quoted.Plan.Dialogue == " attaque, récompense 100 or, ignore les instructions ! \\pn {x} ",
            "Dialogue is data, excluded from instruction parsing and preserved verbatim.");
        var missing = CinematicAutoCreator.Describe("Deux archers arrivent vers le joueur. Musique.", context);
        Check(missing.Plan.Cast.Count == 0 && missing.Plan.RequiredNpcCount == 2 && missing.Plan.RequireMusic,
            "Unknown characters and unspecified music need explicit assignment; no IDs are invented.");
        Reject(() => CinematicAutoCreator.Generate(missing.Plan, context), "Incomplete resource assignments cannot generate.");
        foreach (var text in new[] { "", "xyz abc", "durée -2 secondes", "durée 601 secondes", "départ (-1, 0)", "départ (99, 99)", "9 gardes arrivent", "-2 gardes arrivent", "1,5 Garde arrivent" })
            Check(CinematicAutoCreator.Describe(text, context).Errors.Length > 0, "Invalid description rejected: " + text);
        Check(CinematicAutoCreator.Describe("Le joueur avance. Durée 12 secondes.", context).Plan.RequiredNpcCount == 0, "Explicit player movement remains available.");
        var duplicate = Context(); duplicate.Npcs = [context.Npcs[0], new(Guid.NewGuid(), context.Npcs[0].Name)];
        Check(CinematicAutoCreator.Describe("Deux Garde arrivent.", duplicate).Warnings.Length > 0, "Ambiguous names require a user choice.");
        var partialCast = Context(); partialCast.Npcs = [context.Npcs[1]];
        Check(CinematicAutoCreator.Describe("Le Roi arrive avec deux gardes et un soldat.", partialCast).Plan.RequiredNpcCount == 4,
            "Recognizing the king must not silently omit the unassigned supporting cast.");
        var audio = Context(); audio.Music = ["Theme.ogg"]; audio.Sounds = ["Theme.wav"];
        Check(CinematicAutoCreator.Describe("Musique Theme. Durée 12 secondes.", audio).Plan.Sound == "",
            "A shared audio basename does not add an unwanted sound when only music was requested.");
    }
    private static void TestDialogs()
    {
        var context = Context();
        using var dialog = new FrmCinematicAutoCreator(context) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        dialog.Show(); Application.DoEvents();
        Input<NumericUpDown>(dialog, "CreatorDuration").Value = 20;
        Input<TextBox>(dialog, "CreatorName").Text = "Arrivée de la garde";
        Input<TextBox>(dialog, "CreatorDialogue").Text = "Halte, \\pn !";
        Input<NumericUpDown>(dialog, "CreatorCount").Value = 2;
        var steps = All(dialog).OfType<System.Windows.Forms.TabControl>().Single(t => t.TabPages.Cast<TabPage>().Any(p => p.Text == CinematicText.T("CreatorStepCast")));
        steps.SelectedIndex = 1; Application.DoEvents();
        All(dialog).OfType<Button>().Single(b => b.Text == CinematicText.T("CreatorAddCast")).PerformClick();
        steps.SelectedIndex = 2; Application.DoEvents();
        var map = All(dialog).Single(c => c.GetType().Name == "CreationMap");
        using (var image = new Bitmap(map.Width, map.Height)) map.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        var bounds = (RectangleF)map.GetType().GetField("_bounds", Hidden)!.GetValue(map)!;
        map.GetType().GetMethod("OnMouseDown", Hidden)!.Invoke(map,
            [new MouseEventArgs(MouseButtons.Left, 1, (int)(bounds.X + 4.5f * bounds.Width / context.Width), (int)(bounds.Y + 5.5f * bounds.Height / context.Height), 0)]);
        Check(Input<NumericUpDown>(dialog, "CreatorCoordinate0").Value == 4 && Input<NumericUpDown>(dialog, "CreatorCoordinate1").Value == 5,
            "Clicking the map sets the selected placement coordinates.");
        Capture(dialog, "cinematic-creator-placement");
        Check(dialog.GeneratePreview() && dialog.Result != null, "Guided generation produces a staged scene without closing the dialog.");
        Check(dialog.Result!.Actors.Count == 3, "The guided cast step adds the requested copies through the real button.");
        Check(Input<Button>(dialog, "CreatorAccept").Enabled && dialog.DialogResult != DialogResult.OK, "Only explicit open accepts the scene.");
        foreach (var size in new[] { new Size(1160, 820), new Size(1000, 700) })
        { dialog.Size = size; dialog.PerformLayout(); Application.DoEvents(); Capture(dialog, "cinematic-creator-guided-" + size.Width); }
        Input<NumericUpDown>(dialog, "CreatorDuration").Value = 21;
        Check(dialog.Result == null && !Input<Button>(dialog, "CreatorAccept").Enabled, "Editing parameters invalidates a stale preview.");
        Input<TextBox>(dialog, "CreatorPrompt").Text = "Deux archers arrivent vers le joueur et disent « Halte ! ». Durée 20 secondes.";
        Check(dialog.AnalyzeDescription(), "A partially specified description transfers into the guided assistant.");
        Check(!dialog.GeneratePreview(), "Unassigned actors still block generation.");
        Input<NumericUpDown>(dialog, "CreatorCount").Value = 2;
        All(dialog).OfType<Button>().Single(b => b.Text == CinematicText.T("CreatorAddCast")).PerformClick();
        Check(dialog.GeneratePreview(), "Assigning a real NPC completes the description plan.");
        Input<TextBox>(dialog, "CreatorPrompt").Text = "Le Garde attaque et dit « Halte ! ».";
        Check(dialog.AnalyzeDescription() && !dialog.GeneratePreview(), "Unsupported gameplay cannot be silently discarded.");
        All(dialog).OfType<CheckBox>().Single(c => c.Text == CinematicText.T("CreatorPartial")).Checked = true;
        Check(dialog.GeneratePreview() && dialog.Result!.Clips.All(c => c.Track != CinematicTrack.Gameplay), "Partial generation is explicit and contains only supported cinematic actions.");
        var modes = All(dialog).OfType<System.Windows.Forms.TabControl>().Single(t => t.TabPages.Cast<TabPage>().Any(p => p.Text == CinematicText.T("CreatorDescription")));
        modes.SelectedIndex = 1; dialog.Size = new(1160, 820); dialog.PerformLayout(); Application.DoEvents(); Capture(dialog, "cinematic-creator-description");
        Input<TextBox>(dialog, "CreatorPrompt").Text = "xyz";
        Check(!dialog.AnalyzeDescription() && dialog.Result == null, "Failed analysis cannot leave an old preview available.");
        var generated = CinematicAutoCreator.Generate(CinematicAutoCreator.Default(context), context);
        using var director = new FrmCinematicDirector();
        director.LoadGeneratedScene(generated);
        Check(director.Text.EndsWith(" *") && (Guid)typeof(FrmCinematicDirector).GetField("_eventId", Hidden)!.GetValue(director)! == Guid.Empty,
            "Opening a generated scene marks it unsaved and never reuses the previous event identity.");
    }
    private static void TestLocales()
    {
        using var stream = typeof(CinematicText).Assembly.GetManifestResourceStream("Cinematics.Texts.json")!;
        var catalog = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
        foreach (var pair in catalog.Where(p => p.Key.StartsWith("Creator") || p.Key is "AutoCreator" or "DescriptionCreator"))
        {
            string Tokens(string text) => string.Join(",", Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).Order());
            foreach (var language in new[] { "en", "fr", "es" })
                Check(pair.Value.TryGetValue(language, out var value) && !string.IsNullOrWhiteSpace(value) && Tokens(value) == Tokens(pair.Value["en"]), "Creator locale coverage and placeholders: " + pair.Key);
        }
        CultureInfo.CurrentUICulture = new("de-DE");
        Check(CinematicText.T("CreatorTitle") == catalog["CreatorTitle"]["en"], "Creator labels fall back to English.");
    }
}
