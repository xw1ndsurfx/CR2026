using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Intersect.Framework.Core.Cinematics;
using DrawingPoint = System.Drawing.Point;

namespace Intersect.Editor.Forms.Cinematics;

internal enum CreatorScenario { Arrival, Conversation, Ambush, QuestIntroduction, BossReveal, Victory }
internal sealed record CreatorNpc(Guid Id, string Name) { public override string ToString() => Name; }
internal sealed record CreatorEffect(Guid Id, string Name) { public override string ToString() => Name; }
internal sealed record CreatorCast(CreatorNpc Npc, int Count) { public override string ToString() => Count + " × " + Npc.Name; }

internal sealed class CinematicCreationContext
{
    internal Guid MapId;
    internal string MapName = "";
    internal int Width, Height;
    internal Func<int, int, bool> Blocked = (_, _) => false;
    internal CreatorNpc[] Npcs = [];
    internal CreatorEffect[] Effects = [];
    internal string[] Music = [], Sounds = [];
}

internal sealed class CinematicCreationPlan
{
    internal string Name = "";
    internal CreatorScenario Scenario;
    internal CinematicProjection Projection;
    internal int DurationMs = 12000, DelayMs;
    internal DrawingPoint Entry, Destination, Camera;
    internal bool Move = true;
    internal List<CreatorCast> Cast = [];
    internal string Dialogue = "", Music = "", Sound = "";
    internal Guid EffectId;
    internal int RequiredNpcCount;
    internal bool RequireMusic, RequireEffect;
}

internal sealed record CinematicDescriptionResult(CinematicCreationPlan Plan, string[] Errors, string[] Warnings, bool Recognized);

internal static class CinematicAutoCreator
{
    private static string T(string key) => CinematicText.T(key);
    private static void Require(bool condition, string key) { if (!condition) throw new InvalidOperationException(T(key)); }
    internal static CinematicCreationPlan Default(CinematicCreationContext context)
    {
        Require(context.MapId != Guid.Empty && context.Width > 0 && context.Height > 0, "CreatorMapRequired");
        var center = Nearest(context, new(context.Width / 2, context.Height / 2), []);
        var entry = Nearest(context, new(Math.Max(0, center.X - 4), center.Y), []);
        return new() { Name = T("CreatorScenarioArrival"), Entry = entry, Destination = center, Camera = center };
    }

    internal static CinematicScene Generate(CinematicCreationPlan plan, CinematicCreationContext context)
    {
        Require(context.MapId != Guid.Empty && context.Width > 0 && context.Height > 0, "CreatorMapRequired");
        Require(!string.IsNullOrWhiteSpace(plan.Name) && plan.Name.Length <= 120, "CreatorNameRequired");
        Require(Enum.IsDefined(plan.Scenario), "CreatorScenarioRequired");
        Require(Enum.IsDefined(plan.Projection), "CreatorProjectionRequired");
        Require(plan.DurationMs is >= 6000 and <= 600000 && plan.DelayMs >= 0 && plan.DelayMs < plan.DurationMs, "CreatorDurationRange");
        Require(plan.Cast.All(c => c.Count is > 0 and <= 8 && context.Npcs.Any(n => n.Id == c.Npc.Id)) &&
            plan.Cast.Sum(c => c.Count) <= 8, "CreatorCastLimit");
        Require(plan.Cast.Sum(c => c.Count) >= plan.RequiredNpcCount, "CreatorAssignCharacters");
        Require(Open(context, plan.Entry) && Open(context, plan.Destination) && Inside(context, plan.Camera), "CreatorPositionBlocked");
        Require((!plan.RequireMusic || plan.Music.Length > 0) && (plan.Music.Length == 0 || context.Music.Contains(plan.Music)), "CreatorMusicRequired");
        Require(plan.Sound.Length == 0 || context.Sounds.Contains(plan.Sound), "CreatorSoundRequired");
        Require((!plan.RequireEffect || plan.EffectId != Guid.Empty) &&
            (plan.EffectId == Guid.Empty || context.Effects.Any(e => e.Id == plan.EffectId)), "CreatorEffectRequired");
        var lines = plan.Dialogue.Replace("\r\n", "\n").Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        Require(lines.Length <= 8 && lines.All(line => line.Length <= 2000), "CreatorDialogueLimit");
        var kind = plan.Scenario switch
        {
            CreatorScenario.Arrival => CinematicTemplateKind.CityIntroduction,
            CreatorScenario.Conversation => CinematicTemplateKind.RoyalAudience,
            CreatorScenario.Ambush => CinematicTemplateKind.Invasion,
            CreatorScenario.QuestIntroduction => CinematicTemplateKind.QuestStart,
            CreatorScenario.BossReveal => CinematicTemplateKind.BossEncounter,
            _ => CinematicTemplateKind.Victory,
        };
        var scene = CinematicTemplates.Create(kind, context.MapId, context.Width, context.Height, T);
        scene.Name = plan.Name; scene.DurationMs = plan.DurationMs;
        foreach (var clip in scene.Clips)
        {
            clip.StartMs = (int)((long)clip.StartMs * plan.DurationMs / 12000);
            clip.DurationMs = Math.Min(Math.Max(50, (int)((long)clip.DurationMs * plan.DurationMs / 12000)), plan.DurationMs - clip.StartMs);
            if (clip.Track == CinematicTrack.Camera) { clip.X = plan.Camera.X; clip.Y = plan.Camera.Y; }
        }
        var player = scene.Actors[0];
        player.StartX = plan.Cast.Count == 0 && plan.Move ? plan.Entry.X : plan.Destination.X;
        player.StartY = plan.Cast.Count == 0 && plan.Move ? plan.Entry.Y : plan.Destination.Y;
        var occupiedStarts = new HashSet<DrawingPoint> { new(player.StartX, player.StartY) };
        var occupiedEnds = new HashSet<DrawingPoint> { plan.Destination };
        var moving = new List<(CinematicActor Actor, DrawingPoint Goal)>();
        foreach (var cast in plan.Cast)
        for (var i = 0; i < cast.Count; i++)
        {
            var start = Nearest(context, plan.Entry, occupiedStarts); occupiedStarts.Add(start);
            var goal = Nearest(context, plan.Destination, occupiedEnds); occupiedEnds.Add(goal);
            var actor = new CinematicActor { Name = cast.Npc.Name + (cast.Count > 1 ? " " + (i + 1) : ""),
                Kind = CinematicActorKind.TemporaryNpc, TargetId = Guid.NewGuid(), NPCId = cast.Npc.Id,
                StartX = start.X, StartY = start.Y, VisibleAtStart = false };
            scene.Actors.Add(actor); moving.Add((actor, goal));
            scene.Clips.Add(new() { Track = CinematicTrack.Actors, Name = actor.Name, ActorId = actor.TargetId,
                StartMs = plan.DelayMs, DurationMs = Math.Min(500, plan.DurationMs - plan.DelayMs) });
        }
        if (moving.Count == 0) moving.Add((player, plan.Destination));
        var movementEnd = plan.DelayMs + 500;
        if (plan.Move)
        foreach (var (actor, goal) in moving)
        {
            var path = Path(context, new(actor.StartX, actor.StartY), goal);
            if (path.Count <= 1) continue;
            var duration = Math.Max(1000, (path.Count - 1) * 250);
            movementEnd = Math.Max(movementEnd, plan.DelayMs + 500 + duration);
            scene.Clips.Add(new() { Track = CinematicTrack.Movement, Name = T("Movement") + " · " + actor.Name,
                ActorId = actor.TargetId, StartMs = plan.DelayMs + 500, DurationMs = duration,
                Points = path.Select(p => new CinematicPoint { X = p.X, Y = p.Y }).ToList() });
        }
        var dialogueStart = movementEnd + 300;
        var needed = dialogueStart + lines.Length * 2000 + 1000;
        if (needed > plan.DurationMs)
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, T("CreatorTooShort"), Math.Ceiling(needed / 1000d)));
        for (var i = 0; i < lines.Length; i++)
            scene.Clips.Add(new() { Track = CinematicTrack.Dialogue, Name = T("Dialogue") + " " + (i + 1), Text = lines[i],
                StartMs = dialogueStart + i * 2000, DurationMs = 2000 });
        if (plan.Music.Length > 0) scene.Clips.Add(new() { Track = CinematicTrack.Music, Name = plan.Music, AudioFile = plan.Music, DurationMs = plan.DurationMs });
        if (plan.Sound.Length > 0) scene.Clips.Add(new() { Track = CinematicTrack.Sound, Name = plan.Sound, AudioFile = plan.Sound,
            StartMs = plan.DelayMs, DurationMs = 1000 });
        if (plan.EffectId != Guid.Empty) scene.Clips.Add(new() { Track = CinematicTrack.Animation,
            Name = context.Effects.Single(e => e.Id == plan.EffectId).Name, ResourceId = plan.EffectId,
            X = plan.Destination.X, Y = plan.Destination.Y, StartMs = movementEnd, DurationMs = 1000 });
        var focus = moving[0].Actor;
        foreach (var camera in scene.Clips.Where(c => c.Track == CinematicTrack.Camera && c.FollowActor)) camera.ActorId = focus.TargetId;
        CinematicAuthoring.InitializeProjection(scene, plan.Projection);
        Validate(scene, context);
        return scene;
    }

    internal static void Validate(CinematicScene scene, CinematicCreationContext context)
    {
        try
        {
            CinematicCompiler.Compile(scene, context.Width, context.Height, context.Blocked,
                id => false, id => context.Effects.Any(a => a.Id == id), id => false,
                (file, music) => (music ? context.Music : context.Sounds).Contains(file),
                id => context.Npcs.Any(n => n.Id == id));
        }
        catch (InvalidOperationException error) { throw new InvalidOperationException(T(error.Message), error); }
    }

    internal static bool Inside(CinematicCreationContext context, DrawingPoint p) => p.X >= 0 && p.Y >= 0 && p.X < context.Width && p.Y < context.Height;
    internal static bool Open(CinematicCreationContext context, DrawingPoint p) => Inside(context, p) && !context.Blocked(p.X, p.Y);
    private static IEnumerable<DrawingPoint> Neighbors(DrawingPoint p)
    { yield return new(p.X - 1, p.Y); yield return new(p.X + 1, p.Y); yield return new(p.X, p.Y - 1); yield return new(p.X, p.Y + 1); }
    private static DrawingPoint Nearest(CinematicCreationContext context, DrawingPoint origin, HashSet<DrawingPoint> reserved)
    {
        Require(Inside(context, origin), "CreatorPositionBlocked");
        var queue = new Queue<DrawingPoint>(); var seen = new HashSet<DrawingPoint> { origin }; queue.Enqueue(origin);
        while (queue.TryDequeue(out var p))
        {
            if (Open(context, p) && !reserved.Contains(p)) return p;
            foreach (var next in Neighbors(p)) if (Inside(context, next) && seen.Add(next)) queue.Enqueue(next);
        }
        throw new InvalidOperationException(T("CreatorNoSpace"));
    }
    internal static List<DrawingPoint> Path(CinematicCreationContext context, DrawingPoint start, DrawingPoint goal)
    {
        Require(Open(context, start) && Open(context, goal), "CreatorPositionBlocked");
        var queue = new Queue<DrawingPoint>(); var parent = new Dictionary<DrawingPoint, DrawingPoint> { [start] = start }; queue.Enqueue(start);
        while (queue.TryDequeue(out var p))
        {
            if (p == goal)
            {
                var result = new List<DrawingPoint> { p };
                while (p != start) { p = parent[p]; result.Add(p); }
                result.Reverse(); Require(result.Count <= 1024, "CreatorNoPath"); return result;
            }
            foreach (var next in Neighbors(p))
                if (Open(context, next) && parent.TryAdd(next, p)) queue.Enqueue(next);
        }
        throw new InvalidOperationException(T("CreatorNoPath"));
    }

    private static string Normalize(string text)
    {
        var result = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) result.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        return Regex.Replace(result.ToString(), @"\s+", " ").Trim();
    }
    private static bool Has(string text, string words) => Regex.IsMatch(text, @"\b(?:" + words + @")\b", RegexOptions.CultureInvariant);
    private static bool Mentions(string text, string name)
    {
        var normalized = Normalize(name);
        return normalized.Length > 0 && Regex.IsMatch(text, @"\b" + Regex.Escape(normalized) + @"s?\b", RegexOptions.CultureInvariant);
    }
    internal static CinematicDescriptionResult Describe(string description, CinematicCreationContext context)
    {
        var plan = Default(context); var errors = new List<string>(); var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(description) || description.Length > 8000)
            return new(plan, [T("CreatorDescriptionRequired")], [], false);
        // Quoted dialogue is authored content: preserve it verbatim, and exclude it from instruction parsing.
        var quotePattern = """["«“](?<line>[^"»”]+)["»”]""";
        var quotes = Regex.Matches(description, quotePattern);
        plan.Dialogue = string.Join("\n", quotes.Select(m => m.Groups["line"].Value));
        var instructions = Regex.Replace(description, quotePattern, " ");
        var text = Normalize(instructions);
        var nouns = "gardes?|guards?|guardias?|soldats?|soldiers?|soldados?|pnj|npcs?" +
            string.Concat(context.Npcs.Where(n => n.Name.Length > 0).Select(n => "|" + Regex.Escape(n.Name) + "s?"));
        foreach (Match request in Regex.Matches(instructions, @"(?<![\d.,])(?<n>-?\d+(?:[.,]\d+)?)\s+(?:" + nouns + @")\b", RegexOptions.IgnoreCase))
            if (!int.TryParse(request.Groups["n"].Value, out var count) || count is < 1 or > 8) errors.Add(T("CreatorCastLimit"));
        var recognized = quotes.Count > 0;
        if (Regex.IsMatch(instructions, @"\b2[.,]5\s*d\b", RegexOptions.IgnoreCase))
        { plan.Projection = CinematicProjection.Perspective2_5D; recognized = true; }
        else if (Regex.IsMatch(instructions, @"\b2\s*d\b", RegexOptions.IgnoreCase))
        { plan.Projection = CinematicProjection.Flat2D; recognized = true; }
        var scenarios = new (CreatorScenario Kind, string Words)[]
        {
            (CreatorScenario.Ambush,"embuscade|ambush|emboscada"),
            (CreatorScenario.BossReveal,"boss|boss reveal|revelation|jefe"),
            (CreatorScenario.QuestIntroduction,"quete|quest|mision"),
            (CreatorScenario.Victory,"victoire|victory|victoria"),
            (CreatorScenario.Conversation,"dialogue|conversation|dialogo|discussion|parlent|talk"),
            (CreatorScenario.Arrival,"arrive|arrivent|arrivee|arrival|arrives|llega|llegan|arrivent"),
        };
        foreach (var scenario in scenarios)
            if (Has(text, scenario.Words)) { plan.Scenario = scenario.Kind; recognized = true; break; }
        plan.Name = T("CreatorScenario" + plan.Scenario);
        plan.Move = Has(text, "arrive|arrivent|avance|avancent|marche|marchent|entre|entrent|approche|approchent|rejoint|move|moves|walk|walks|enter|enters|arrives|arrive|approach|leave|depart|llega|llegan|avanza|avanzan|camina|caminan|entra|entran|sale|salen");
        if (Has(text, "sans deplacement|ne bouge pas|sans mouvement|stay still|without movement|sin movimiento")) plan.Move = false;
        recognized |= plan.Move || Has(text, "camera|cadrage|zoom|camara|plan large|gros plan");
        var countMatch = Regex.Match(text, @"\b(?<n>[1-8]|un|une|deux|trois|quatre|cinq|six|sept|huit|one|two|three|four|five|six|seven|eight|uno|una|dos|tres|cuatro|cinco|seis|siete|ocho)\s+");
        int Count()
        {
            var word = countMatch.Groups["n"].Value;
            return int.TryParse(word, out var n) ? n : word switch
            { "deux" or "two" or "dos" => 2, "trois" or "three" or "tres" => 3, "quatre" or "four" or "cuatro" => 4,
              "cinq" or "five" or "cinco" => 5, "six" or "seis" => 6, "sept" or "seven" or "siete" => 7, "huit" or "eight" or "ocho" => 8, _ => 1 };
        }
        foreach (var group in context.Npcs.GroupBy(n => Normalize(n.Name)))
        {
            if (!Mentions(text, group.First().Name)) continue;
            recognized = true;
            if (group.Count() != 1) { warnings.Add(T("CreatorAmbiguousNpc") + " " + group.First().Name); continue; }
            var npc = group.Single();
            var ownCount = Regex.Match(text, @"\b(?<n>[1-8]|un|une|deux|trois|quatre|cinq|six|sept|huit|one|two|three|four|five|six|seven|eight|uno|una|dos|tres|cuatro|cinco|seis|siete|ocho)\s+" + Regex.Escape(Normalize(npc.Name)) + @"s?\b");
            var saved = countMatch; if (ownCount.Success) countMatch = ownCount;
            plan.Cast.Add(new(npc, ownCount.Success ? Count() : 1)); countMatch = saved;
        }
        var playerMoves = Regex.IsMatch(text, @"\b(?:joueur\s+(?:avance|marche|arrive|entre)|player\s+(?:walks?|moves?|arrives?|enters?)|jugador\s+(?:avanza|camina|llega|entra))\b");
        if (plan.Move && plan.Cast.Count == 0 && !playerMoves)
        { plan.RequiredNpcCount = countMatch.Success ? Count() : 1; warnings.Add(T("CreatorAssignCharacters")); }
        if (Regex.IsMatch(text, @"\b(?:9|[1-9]\d+)\s+(?:gardes?|guards?|guardias?|soldats?|soldiers?|soldados?|pnj|npcs?)\b"))
            errors.Add(T("CreatorCastLimit"));
        var unresolvedCount = 0;
        foreach (Match role in Regex.Matches(text, @"\b(?<n>[1-8]|un|une|deux|trois|quatre|cinq|six|sept|huit|one|two|three|four|five|six|seven|eight|uno|una|dos|tres|cuatro|cinco|seis|siete|ocho)\s+(?<role>gardes?|guards?|guardias?|soldats?|soldiers?|soldados?|rois?|kings?|reyes|pnj|npcs?)\b"))
        {
            var noun = role.Groups["role"].Value.TrimEnd('s');
            if (plan.Cast.Any(c => Normalize(c.Npc.Name).Split(' ').Any(word => word.TrimEnd('s') == noun))) continue;
            var saved = countMatch; countMatch = role; unresolvedCount += Count(); countMatch = saved;
        }
        if (unresolvedCount > 0)
        {
            plan.RequiredNpcCount = Math.Max(plan.RequiredNpcCount, plan.Cast.Sum(c => c.Count) + unresolvedCount);
            if (!warnings.Contains(T("CreatorAssignCharacters"))) warnings.Add(T("CreatorAssignCharacters"));
        }
        foreach (var effect in context.Effects)
            if (Mentions(text, effect.Name))
            {
                if (plan.EffectId != Guid.Empty) { errors.Add(T("CreatorOneEffect")); break; }
                plan.EffectId = effect.Id; recognized = true;
            }
        bool AudioMention(string file, string category, string opposite)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(file);
            if (!Mentions(text, file) && !Mentions(text, name)) return false;
            return Regex.IsMatch(text, @"\b(?:" + category + @")\s+(?:(?:le|la|the|el)\s+)?" + Regex.Escape(Normalize(name)) + @"\b") || !Has(text, opposite);
        }
        foreach (var music in context.Music)
            if (AudioMention(music, "musique|music|musica", "son|sound|sonido|bruit|sfx"))
            { if (plan.Music.Length > 0) errors.Add(T("CreatorOneMusic")); plan.Music = music; recognized = true; }
        foreach (var sound in context.Sounds)
            if (AudioMention(sound, "son|sound|sonido|bruit|sfx", "musique|music|musica"))
            { if (plan.Sound.Length > 0) errors.Add(T("CreatorOneSound")); plan.Sound = sound; recognized = true; }
        plan.RequireMusic = Has(text, "musique|music|musica");
        plan.RequireEffect = Has(text, "effet|effect|efecto");
        if (plan.RequireMusic && plan.Music.Length == 0) warnings.Add(T("CreatorMusicRequired"));
        if (plan.RequireEffect && plan.EffectId == Guid.Empty) warnings.Add(T("CreatorEffectRequired"));
        var duration = Regex.Match(description, @"(?:dur[eé]e|duration|duraci[oó]n)\s*[:=]?\s*(?<n>-?\d+(?:[.,]\d+)?)\s*(?:s\b|secondes?\b|seconds?\b|segundos?\b)", RegexOptions.IgnoreCase);
        var delay = Regex.Match(description, @"(?:apr[eè]s|after|despu[eé]s de|attendre|wait|esperar)\s*(?<n>-?\d+(?:[.,]\d+)?)\s*(?:s\b|secondes?\b|seconds?\b|segundos?\b)", RegexOptions.IgnoreCase);
        int Milliseconds(Match match) => decimal.TryParse(match.Groups["n"].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) && n is >= 0 and <= 600 ? (int)(n * 1000) : -1;
        if (duration.Success) { plan.DurationMs = Milliseconds(duration); recognized = true; }
        if (delay.Success) { plan.DelayMs = Milliseconds(delay); recognized = true; }
        if (plan.DurationMs is < 6000 or > 600000 || plan.DelayMs < 0 || plan.DelayMs >= plan.DurationMs) errors.Add(T("CreatorDurationRange"));
        DrawingPoint? Position(string prefix)
        {
            var match = Regex.Match(description, @"(?:" + prefix + @")\s*[:=]?\s*\(?\s*(?<x>-?\d+)\s*[,;]\s*(?<y>-?\d+)\s*\)?", RegexOptions.IgnoreCase);
            if (!match.Success) return null;
            recognized = true;
            if (!int.TryParse(match.Groups["x"].Value, out var x) || !int.TryParse(match.Groups["y"].Value, out var y)) { errors.Add(T("CreatorPositionBlocked")); return null; }
            var p = new DrawingPoint(x, y); if (!Inside(context, p)) errors.Add(T("CreatorPositionBlocked")); return p;
        }
        if (Position("d[eé]part|entr[eé]e|start|from|inicio|entrada") is { } entry) plan.Entry = entry;
        if (Position("destination|vers|towards?|to|destino|hacia") is { } destination) plan.Destination = destination;
        if (Position("cam[eé]ra|camera|c[aá]mara") is { } camera) plan.Camera = camera;
        if (Has(text, "attaque|attaquent|attack|attacks|ataca|atacan|tue|kill|mata|recompense|reward|recompensa|teleporte|teleport|danse|dance|baila|naval|flotte|fleet|flota"))
            warnings.Add(T("CreatorUnsupportedActions"));
        if (!recognized) errors.Add(T("CreatorDescriptionUnrecognized"));
        return new(plan, errors.Distinct().ToArray(), warnings.Distinct().ToArray(), recognized);
    }
}
