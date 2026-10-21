using MessagePack;
namespace Intersect.Network.Packets.PartyLobbies;
public enum LobbyActivity { Dungeon, Invasion, WorldBoss, Alchemy, Poker, Blackjack, Roulette, Lumberjack, HouseParty, Looting, Cooking, Naval }
public enum LobbyAction { Browse, Create, Join, Close, Travel, Leave }
[MessagePackObject]
public partial class PartyLobbyRequestPacket : IntersectPacket
{
    [Key(0)] public LobbyAction Action { get; set; }
    [Key(1)] public Guid LobbyId { get; set; }
    [Key(2)] public LobbyActivity Activity { get; set; }
    [Key(3)] public Guid DungeonId { get; set; }
    [Key(4)] public string Title { get; set; } = "";
}
[MessagePackObject]
public sealed class PartyLobbyEntry
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public Guid HostId { get; set; }
    [Key(2)] public string HostName { get; set; } = "";
    [Key(3)] public string Title { get; set; } = "";
    [Key(4)] public LobbyActivity Activity { get; set; }
    [Key(5)] public Guid DungeonId { get; set; }
    [Key(6)] public string DungeonName { get; set; } = "";
    [Key(7)] public int Players { get; set; }
    [Key(8)] public int Capacity { get; set; }
    [Key(9)] public bool Started { get; set; }
    [Key(10)] public string[] Members { get; set; } = [];
}
[MessagePackObject]
public sealed class LobbyDungeonChoice
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public string Name { get; set; } = "";
    [Key(2)] public int MinimumLevel { get; set; }
    [Key(3)] public int MaximumLevel { get; set; }
}
[MessagePackObject]
public partial class PartyLobbyStatePacket : IntersectPacket
{
    [Key(0)] public PartyLobbyEntry[] Lobbies { get; set; } = [];
    [Key(1)] public LobbyDungeonChoice[] Dungeons { get; set; } = [];
    [Key(2)] public Guid CurrentLobbyId { get; set; }
    // Localization catalog key. Authored names/titles are never interpreted as keys.
    [Key(3)] public string Message { get; set; } = "";
}
