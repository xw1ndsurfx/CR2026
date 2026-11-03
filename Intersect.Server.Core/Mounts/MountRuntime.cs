using Intersect.Framework.Core.Mounts;
using Intersect.Server.Entities;
namespace Intersect.Server.Mounts;
internal static class MountRuntime
{
    private static readonly object Gate = new();
    private static MountConfiguration? _current;
    internal static MountConfiguration Current { get { lock (Gate) return _current ??= Load(); } }
    private static MountConfiguration Load()
    {
        if (!File.Exists("resources/mounts.json")) return MountConfiguration.Prototype();
        try { return MountConfiguration.FromJson(File.ReadAllText("resources/mounts.json")); }
        catch (Exception) { return new MountConfiguration { Enabled = false }; }
    }
    internal static void Save(string json)
    {
        var config = MountConfiguration.FromJson(json);
        lock (Gate)
        {
            Directory.CreateDirectory("resources"); File.WriteAllText("resources/mounts.json.tmp", config.ToJson());
            File.Move("resources/mounts.json.tmp", "resources/mounts.json", true); _current = config;
        }
        foreach (var player in Player.OnlinePlayersSnapshot())
        {
            try
            {
                lock (player.EntityLock)
                {
                    player.UpdateMount();
                    if (player.ActiveMountId != Guid.Empty) Intersect.Server.Networking.PacketSender.SendEntityDataToProximity(player);
                    player.SendMountState();
                }
            }
            catch (Exception) { /* Persistence already succeeded; a disconnect must not turn its acknowledgement into an error. */ }
        }
    }
    internal static MountDefinition? Find(Guid id) => Current.Enabled ? Current.Mounts.FirstOrDefault(m => m.Id == id) : null;
    internal static bool Owns(Player player, MountDefinition definition) => definition.Starter || player.PetCollection.Mounts.Owned.Contains(definition.Id);
}
