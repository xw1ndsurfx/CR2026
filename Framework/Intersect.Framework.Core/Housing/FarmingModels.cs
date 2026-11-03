using Intersect.Framework.Core.Professions;

namespace Intersect.Framework.Core.Housing;

public sealed class FarmCrop
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Wheat";
    public Guid SeedItemId { get; set; }
    public Guid HarvestItemId { get; set; }
    public int Seconds { get; set; } = 1800;
    public int Yield { get; set; } = 10;
    public int Experience { get; set; } = 20;
    public int RequiredLevel { get; set; } = 1;
    public string Tileset { get; set; } = "";
    public int SourceX { get; set; }
    public int SourceY { get; set; }
    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 96 &&
        SeedItemId != Guid.Empty && HarvestItemId != Guid.Empty && Seconds is >= 60 and <= 604800 &&
        Yield is >= 1 and <= 1000 && Experience is >= 0 and <= 1000 && RequiredLevel is >= 1 and <= 100 &&
        Tileset.Length <= 260 && Tileset == Path.GetFileName(Tileset) && SourceX is >= 0 and <= 32768 && SourceY is >= 0 and <= 32768;
}

public sealed class FarmConfiguration
{
    [System.ComponentModel.Browsable(false)]
    public long ProductionEpochUtc { get; set; }
    public bool Enabled { get; set; }
    public Guid ExteriorMapId { get; set; }
    public int EntryX { get; set; } = 10;
    public int EntryY { get; set; } = 10;
    public int FieldX { get; set; } = 4;
    public int FieldY { get; set; } = 4;
    public int MaximumWorkers { get; set; } = 2;
    public int StorageCapacity { get; set; } = 500;
    public Guid CurrencyItemId { get; set; }
    public int HiringPrice { get; set; } = 100;
    public int WagePerCycle { get; set; } = 5;
    public string WorkerSprite { get; set; } = "";
    public List<FarmCrop> Crops { get; set; } = [];
    public bool IsValid => !Enabled || ExteriorMapId != Guid.Empty && CurrencyItemId != Guid.Empty &&
        EntryX is >= 0 and <= 255 && EntryY is >= 0 and <= 255 && FieldX is >= 0 and <= 255 && FieldY is >= 0 and <= 255 &&
        MaximumWorkers is >= 1 and <= 8 && StorageCapacity is >= 1 and <= 10000 &&
        HiringPrice is >= 1 and <= 1000000 && WagePerCycle is >= 1 and <= 10000 &&
        WorkerSprite.Length is > 0 and <= 260 && WorkerSprite == Path.GetFileName(WorkerSprite) &&
        Crops.Count is >= 1 and <= 16 && Crops.All(c => c != null && c.IsValid) && Crops.Select(c => c.Id).Distinct().Count() == Crops.Count;
}

public sealed class FarmPlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CropId { get; set; }
    public bool Experienced { get; set; }
    public bool Active { get; set; }
    // A started cycle keeps its terms even when the editor changes the crop.
    public long StartedUtc { get; set; }
    public long DueUtc { get; set; }
    public Guid PendingItemId { get; set; }
    public int PendingQuantity { get; set; }
    public int PendingExperience { get; set; }
}

public sealed class FarmProfile
{
    public bool Learned { get; set; }
    public long Experience { get; set; }
    public long LastUtc { get; set; }
    public int Wallet { get; set; }
    public Guid CurrencyItemId { get; set; }
    public long Harvested { get; set; }
    public long WagesPaid { get; set; }
    public List<FarmPlot> Plots { get; set; } = [];
    public Dictionary<Guid, int> Seeds { get; set; } = [];
    public Dictionary<Guid, int> Storage { get; set; } = [];

    // UTC event simulation is shared by live updates and offline catch-up. It never
    // credits elapsed time twice, and never banks idle time for future supplies.
    public void Advance(FarmConfiguration config, long now)
    {
        if (now < LastUtc || now < config.ProductionEpochUtc) return;
        var time = Math.Max(LastUtc == 0 ? now : LastUtc, config.ProductionEpochUtc);
        if (!config.Enabled) { LastUtc = now; return; }
        while (true)
        {
            foreach (var plot in Plots.Where(p => p.DueUtc > 0 && p.DueUtc <= time))
            {
                Storage[plot.PendingItemId] = Storage.GetValueOrDefault(plot.PendingItemId) + plot.PendingQuantity;
                Harvested += plot.PendingQuantity;
                Experience = FarmingProfession.Definition.ClampExperience(Experience + plot.PendingExperience);
                plot.DueUtc = 0; plot.PendingQuantity = 0; plot.PendingExperience = 0;
            }
            foreach (var plot in Plots.Take(config.MaximumWorkers).Where(p => p.Active && p.DueUtc == 0))
            {
                var crop = config.Crops.FirstOrDefault(c => c.Id == plot.CropId);
                if (crop == null || FarmingProfession.Definition.LevelForExperience(Experience) < crop.RequiredLevel) continue;
                var wage = config.WagePerCycle * (plot.Experienced ? 2 : 1);
                var quantity = crop.Yield + (plot.Experienced ? Math.Max(1, crop.Yield / 4) : 0);
                var occupied = Storage.Values.Sum() + Plots.Sum(p => p.PendingQuantity);
                if (CurrencyItemId != config.CurrencyItemId || Wallet < wage || Seeds.GetValueOrDefault(crop.SeedItemId) < 1 || occupied + quantity > config.StorageCapacity) continue;
                Wallet -= wage; WagesPaid += wage; Seeds[crop.SeedItemId]--;
                plot.StartedUtc = time;
                plot.DueUtc = time + (long)crop.Seconds * 1000 * (plot.Experienced ? 80 : 100) / 100;
                plot.PendingItemId = crop.HarvestItemId; plot.PendingQuantity = quantity; plot.PendingExperience = crop.Experience;
            }
            var next = Plots.Where(p => p.DueUtc > time).Select(p => p.DueUtc).DefaultIfEmpty(long.MaxValue).Min();
            if (next > now || next == long.MaxValue) break;
            time = next;
        }
        LastUtc = now;
    }

    public string Status(FarmPlot plot, FarmConfiguration config)
    {
        if (plot.DueUtc > 0) return "Working";
        if (!plot.Active) return "Paused";
        var crop = config.Crops.FirstOrDefault(c => c.Id == plot.CropId);
        if (crop == null) return "Choose a crop";
        if (FarmingProfession.Definition.LevelForExperience(Experience) < crop.RequiredLevel) return "Farming level too low";
        if (CurrencyItemId != config.CurrencyItemId || Wallet < config.WagePerCycle * (plot.Experienced ? 2 : 1)) return "Salary fund empty";
        if (Seeds.GetValueOrDefault(crop.SeedItemId) < 1) return "No seeds";
        return "Granary full";
    }
}

public static class FarmingProfession
{
    public static readonly Guid Id = Guid.Parse("f0c40000-0000-4000-8000-000000000001");
    public static ProfessionDefinition Definition => new() { Id = Id, Name = "Farmer", Description = "Manage crops and hired farm workers.", MaximumLevel = 100, BaseExperience = 100, ExperienceGrowth = 1.08 };
    public static void Include(ProfessionConfiguration config) => config.Professions = config.Professions.Where(p => p.Id != Id).Append(Definition).ToArray();
}
