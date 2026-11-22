using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;

namespace Intersect.Server.Naval;

internal sealed class NavalExpeditionMember
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = "";
    public Guid ShipId { get; set; }
    public bool Ready { get; set; }
    public bool Claimed { get; set; }
    public bool Dismissed { get; set; }
    public long MissingSince { get; set; }
}

internal sealed class NavalExpeditionRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeaderId { get; set; }
    public long Created { get; set; }
    public long Deadline { get; set; }
    public NavalExpeditionDefinition Definition { get; set; } = new();
    public List<NavalExpeditionMember> Members { get; set; } = [];
    public NavalExpeditionStatus Status { get; set; }
    public string Failure { get; set; } = "";
    public int StageIndex { get; set; }
    public int Progress { get; set; }
    public HashSet<Guid> Evidence { get; set; } = [];
    [JsonIgnore] public NavalExpeditionStage? Stage => Definition.Stages.ElementAtOrDefault(StageIndex);
    [JsonIgnore] public Guid[] Ships => Members.Select(m => m.ShipId).Distinct().ToArray();

    public bool NeedsAttention(Guid player) => Members.Any(m => m.PlayerId == player && !m.Dismissed &&
        (Status != NavalExpeditionStatus.Completed || !m.Claimed));

    public bool RecordVictory(NavalExpeditionObjective kind, Guid map, Guid target, Guid evidence, int expectedStage, long now)
    {
        if (kind is not (NavalExpeditionObjective.DefeatPatrol or NavalExpeditionObjective.DefeatMonster) ||
            Status != NavalExpeditionStatus.Active || now >= Deadline || expectedStage != StageIndex ||
            Stage is not {} stage || stage.Objective != kind || stage.MapId != map || stage.TargetId != target ||
            evidence == Guid.Empty || !Evidence.Add(evidence)) return false;
        if (++Progress >= stage.Quantity) Advance();
        return true;
    }

    public void Advance()
    {
        if (Status != NavalExpeditionStatus.Active || Stage == null) return;
        StageIndex++; Progress = 0;
        if (StageIndex == Definition.Stages.Length) Status = NavalExpeditionStatus.Completed;
    }

    public void Fail(string reason) { Status = NavalExpeditionStatus.Failed; Failure = reason; }

    public bool IsValid(int width, int height) => Id != Guid.Empty && LeaderId != Guid.Empty && Created > 0 && Deadline >= Created &&
        Enum.IsDefined(Status) && Definition?.IsValid(width, height) == true && Definition.Stages.Length > 0 &&
        Members is { Count: >= 2 and <= 32 } && Members.All(m => m != null && m.PlayerId != Guid.Empty && m.ShipId != Guid.Empty &&
            m.Name != null && m.Name.Length <= 128 && m.MissingSince >= 0) && Members.Any(m => m.PlayerId == LeaderId) &&
        Members.Select(m => m.PlayerId).Distinct().Count() == Members.Count && Definition.AcceptsFleet(Members.Count, Ships.Length) &&
        StageIndex >= 0 && StageIndex <= Definition.Stages.Length &&
        (Status != NavalExpeditionStatus.Completed || StageIndex == Definition.Stages.Length) &&
        (Status != NavalExpeditionStatus.Active || StageIndex < Definition.Stages.Length) &&
        (Status != NavalExpeditionStatus.Preparing || StageIndex == 0 && Progress == 0) && Progress is >= 0 and <= 20 &&
        Evidence is { Count: <= 320 } && Failure != null && Failure.Length <= 200;
}

internal sealed class NavalExpeditionStore(string path)
{
    internal const int MaximumRuns = 64;
    private const int MaximumBytes = 8_000_000;
    private sealed class Document
    {
        public int Version { get; set; } = 1;
        public NavalExpeditionRun[] Runs { get; set; } = [];
    }

    internal Dictionary<Guid, NavalExpeditionRun> Load(int width, int height)
    {
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Expedition journal too large.");
        var document = JsonConvert.DeserializeObject<Document>(File.ReadAllText(path),
            new JsonSerializerSettings { MaxDepth = 16, TypeNameHandling = TypeNameHandling.None });
        if (document?.Version != 1 || document.Runs == null || document.Runs.Length > MaximumRuns ||
            document.Runs.Any(r => r?.IsValid(width, height) != true) || document.Runs.Select(r => r.Id).Distinct().Count() != document.Runs.Length ||
            document.Runs.Where(r => r.Status is NavalExpeditionStatus.Preparing or NavalExpeditionStatus.Active or NavalExpeditionStatus.Completed)
                .SelectMany(r => r.Members.Where(m => r.Status != NavalExpeditionStatus.Completed || !m.Claimed).Select(m => m.PlayerId))
                .GroupBy(id => id).Any(g => g.Count() > 1))
            throw new InvalidDataException("Invalid expedition journal. Existing data was preserved.");
        return document.Runs.ToDictionary(r => r.Id);
    }

    internal void Save(IEnumerable<NavalExpeditionRun> runs) => NavalConfigurationStore.WriteAtomic(path,
        JsonConvert.SerializeObject(new Document { Runs = runs.ToArray() }, Formatting.Indented), MaximumBytes);
}
