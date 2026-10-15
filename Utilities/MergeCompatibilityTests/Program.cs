using System.Reflection;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Achievements;
using Intersect.Network.Packets.Server;
using MessagePack;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

Check((int)AchievementObjectiveType.LockpickingSuccesses == 8 &&
      (int)AchievementObjectiveType.LockpickingPerfects == 9 &&
      (int)AchievementObjectiveType.LockpickingDifficultyFive == 10 &&
      (int)AchievementObjectiveType.LockpickingBrokenPicks == 11 &&
      (int)AchievementObjectiveType.LockpickingFastPicks == 12 &&
      (int)AchievementObjectiveType.LockpickingNoBreakPicks == 13,
    "Existing lockpicking achievement identifiers changed");
Check((int)AchievementObjectiveType.DungeonCompletions == 14 &&
      (int)AchievementObjectiveType.InvasionCompletions == 15,
    "New achievement identifiers overlap saved lockpicking objectives");
Console.WriteLine("PASS saved achievement numeric identifiers remain compatible");

foreach (var objective in new[] { AchievementObjectiveType.LockpickingSuccesses,
             AchievementObjectiveType.DungeonCompletions, AchievementObjectiveType.InvasionCompletions })
{
    var id = Guid.NewGuid();
    var target = Guid.NewGuid();
    var configuration = new AchievementConfiguration
    {
        Achievements = [new() { Id = id, ObjectiveType = objective, TargetId = target, TargetAmount = 5 }],
    };
    var restored = AchievementConfiguration.FromJson(configuration.ToJson()).Find(id);
    Check(restored?.ObjectiveType == objective && restored.TargetId == target && restored.TargetAmount == 5,
        "Achievement type or target lost during save/reopen");
}
Console.WriteLine("PASS old and new achievements preserve their target on save/reopen");

var properties = typeof(PlayerEntityPacket).GetProperties()
    .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()))
    .Where(entry => entry.Key?.IntKey != null).ToArray();
Check(properties.GroupBy(entry => entry.Key!.IntKey).All(group => group.Count() == 1),
    "Player packet contains duplicate numeric keys");
Check(properties.Single(entry => entry.Property.Name == nameof(PlayerEntityPacket.Appearance)).Key!.IntKey == 31 &&
      properties.Single(entry => entry.Property.Name == nameof(PlayerEntityPacket.IsAfk)).Key!.IntKey == 32 &&
      properties.Single(entry => entry.Property.Name == nameof(PlayerEntityPacket.BadgeId)).Key!.IntKey == 33,
    "Badge displaced appearance or AFK packet fields");
Console.WriteLine("PASS badge packet key is unique and preserves appearance and AFK");

var packet = new PlayerEntityPacket
{
    BadgeId = 3, IsAfk = true,
    Appearance = new CharacterAppearance { HairStyle = "cc_hair_test" },
};
var decoded = MessagePackSerializer.Deserialize<PlayerEntityPacket>(MessagePackSerializer.Serialize(packet));
Check(decoded.BadgeId == 3 && decoded.IsAfk && decoded.Appearance.HairStyle == "cc_hair_test",
    "Badge, appearance or AFK lost during real packet serialization");
Console.WriteLine("PASS MessagePack round-trip preserves badge, appearance and AFK together");
Console.WriteLine("4/4 merge compatibility groups passed.");
