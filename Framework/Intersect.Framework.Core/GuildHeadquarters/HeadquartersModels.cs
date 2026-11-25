using Newtonsoft.Json;

namespace Intersect.Framework.Core.GuildHeadquarters;

public enum HeadquartersAction : byte { Open, Enter, Leave, OpenBank, Upgrade, Invite, AcceptInvitation, DeclineInvitation, EndVisits }

public sealed class HeadquartersTier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "guild_hq_lodge_v1.png";
    public Guid MapId { get; set; }
    public int EntryX { get; set; } = 10;
    public int EntryY { get; set; } = 10;
    public int BankX { get; set; } = 12;
    public int BankY { get; set; } = 10;
    public Guid CostItemId { get; set; }
    public int CostQuantity { get; set; }
    public int MinimumMembers { get; set; } = 1;
    public int BankSlots { get; set; }
    public bool IsValid(int width, int height) => Id != Guid.Empty && Name != null && Name.Length <= 100 &&
        Description != null && Description.Length <= 2000 && HeadquartersAssets.File(Icon, ".png") && MapId != Guid.Empty &&
        EntryX >= 0 && EntryX < width && EntryY >= 0 && EntryY < height &&
        BankX >= 0 && BankX < width && BankY >= 0 && BankY < height &&
        CostQuantity is >= 0 and <= 1000000000 && (CostItemId == Guid.Empty ? CostQuantity == 0 : CostQuantity > 0) &&
        MinimumMembers is >= 1 and <= 1000 && BankSlots is >= 0 and <= 10000;
}

public sealed class HeadquartersConfiguration
{
    public bool Enabled { get; set; }
    // Rank 0 is the owner. Lower numbers are more privileged, as in native guild permissions.
    public int ManagingRank { get; set; }
    public int InvitingRank { get; set; }
    public bool AllowVisitors { get; set; } = true;
    public int VisitMinutes { get; set; } = 30;
    public HeadquartersAssets Assets { get; set; } = new();
    public HeadquartersTier[] Tiers { get; set; } = [];
    public bool IsValid(int width, int height) => Assets is { IsValid: true } && ManagingRank is >= 0 and <= 99 && InvitingRank is >= 0 and <= 99 && VisitMinutes is >= 1 and <= 120 && Tiers != null && Tiers.Length <= 12 &&
        (!Enabled || Tiers.Length > 0) && Tiers.All(t => t?.IsValid(width, height) == true && (!Enabled || !string.IsNullOrWhiteSpace(t.Name))) &&
        Tiers.Select(t => t.Id).Distinct().Count() == Tiers.Length && Tiers.Select(t => t.MapId).Distinct().Count() == Tiers.Length &&
        (Tiers.Length == 0 || Tiers[0].CostQuantity == 0 && Tiers[0].MinimumMembers == 1 && Tiers[0].BankSlots == 0) &&
        Tiers.Zip(Tiers.Skip(1)).All(pair => pair.Second.BankSlots >= pair.First.BankSlots);
}

public sealed class HeadquartersProfile
{
    public Guid[] Upgrades { get; set; } = [];
    public long Revision { get; set; }
    public int TierIndex(HeadquartersConfiguration configuration)
    {
        for (var index = 0; index < Upgrades.Length; index++)
            if (configuration.Tiers.ElementAtOrDefault(index + 1)?.Id != Upgrades[index])
                throw new InvalidDataException("Headquarters progress does not match saved tiers.");
        return Upgrades.Length;
    }
    public static HeadquartersProfile Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        if (json.Length > 10000) throw new InvalidDataException("Invalid headquarters progress.");
        var result = JsonConvert.DeserializeObject<HeadquartersProfile>(json, new JsonSerializerSettings { MaxDepth = 8, TypeNameHandling = TypeNameHandling.None });
        if (result == null || result.Upgrades == null || result.Upgrades.Length > 12 || result.Upgrades.Any(id => id == Guid.Empty) ||
            result.Upgrades.Distinct().Count() != result.Upgrades.Length || result.Revision < 0)
            throw new InvalidDataException("Invalid headquarters progress.");
        return result;
    }
}

public sealed class HeadquartersVisit
{
    public Guid GuildId { get; set; }
    public Guid InstanceId { get; set; }
    public Guid MapId { get; set; }
    public Guid ReturnMapId { get; set; }
    public int ReturnX { get; set; }
    public int ReturnY { get; set; }
}

public sealed class HeadquartersView
{
    public bool Enabled { get; set; }
    public Guid GuildId { get; set; }
    public string GuildName { get; set; } = "";
    public bool Inside { get; set; }
    public Guid MapId { get; set; }
    public Guid InstanceId { get; set; }
    public HeadquartersAssets Assets { get; set; } = new();
    public bool CanManage { get; set; }
    public bool CanInvite { get; set; }
    public bool Visitor { get; set; }
    public int Visitors { get; set; }
    public int VisitMinutes { get; set; }
    public string ConfigurationRevision { get; set; } = "";
    public bool NearBank { get; set; }
    public long Revision { get; set; }
    public int TierNumber { get; set; }
    public int MemberCount { get; set; }
    public int BankSlots { get; set; }
    public HeadquartersTier? Current { get; set; }
    public HeadquartersTier? Next { get; set; }
    public string CostItemName { get; set; } = "";
    public long AvailableQuantity { get; set; }
    public string Message { get; set; } = "";
    public bool BankOpened { get; set; }
}
