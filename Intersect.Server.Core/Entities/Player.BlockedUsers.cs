using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Intersect.Server.Entities;

/// <summary>Persistent per-character privacy preference; never trust a client-supplied id as the owner.</summary>
public partial class Player
{
    private const int MaxBlockedUsers = 200;

    [Column("BlockedUsers")]
    [JsonIgnore]
    public string BlockedUsersJson { get; set; } = "{}";

    public Dictionary<Guid, string> ReadBlockedUsers()
    {
        if (string.IsNullOrWhiteSpace(BlockedUsersJson))
            return [];

        try
        {
            return JsonConvert.DeserializeObject<Dictionary<Guid, string>>(BlockedUsersJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public bool Blocks(Guid targetId) =>
        targetId != Guid.Empty && ReadBlockedUsers().ContainsKey(targetId);

    public bool TryBlock(Player? target)
    {
        if (target == null || target.Id == Guid.Empty || target.Id == Id)
            return false;

        var entries = ReadBlockedUsers();
        if (!entries.ContainsKey(target.Id) && entries.Count >= MaxBlockedUsers)
            return false;

        entries[target.Id] = target.Name;
        BlockedUsersJson = JsonConvert.SerializeObject(entries);
        Save();
        return true;
    }

    public bool TryUnblock(Guid targetId)
    {
        if (targetId == Guid.Empty)
            return false;

        var entries = ReadBlockedUsers();
        if (!entries.Remove(targetId))
            return false;

        BlockedUsersJson = JsonConvert.SerializeObject(entries);
        Save();
        return true;
    }
}
