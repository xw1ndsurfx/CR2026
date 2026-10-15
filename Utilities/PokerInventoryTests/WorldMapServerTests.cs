using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.WorldMaps;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Networking;

internal static class WorldMapServerTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var online = (ConcurrentDictionary<Guid, Player>)typeof(Player)
            .GetField("OnlinePlayersById", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var map = Guid.NewGuid();
        var instance = Guid.NewGuid();
        var spawned = new List<Player>();
        Player NewPlayer(string name)
        {
            var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
            GC.SuppressFinalize(player);
            player.Id = Guid.NewGuid(); player.Name = name; player.MapId = map; player.MapInstanceId = instance;
            player.X = 12; player.Y = 17; player.Party = []; player.CachedFriends = []; player.CachedStatuses = [];
            online[player.Id] = player; spawned.Add(player); return player;
        }
        try
        {
            var self = NewPlayer("Self"); var friend = NewPlayer("Friend"); var member = NewPlayer("Party");
            var stranger = NewPlayer("Stranger"); var hidden = NewPlayer("Hidden friend");
            var separate = NewPlayer("Different instance"); var distant = NewPlayer("Different grid");
            self.Party = [self, member];
            self.GuildId = Guid.NewGuid(); stranger.GuildId = self.GuildId;
            self.CachedFriends = new[] { friend, member, hidden, separate, distant }.ToDictionary(p => p.Id, p => p.Name);
            hidden.HideEntity = true; separate.MapInstanceId = Guid.NewGuid(); distant.MapId = Guid.NewGuid();
            var method = typeof(PacketSender).GetMethod("BuildWorldMapMarkers", BindingFlags.Static | BindingFlags.NonPublic)!;
            WorldMapSocialMarker[] Build() => (WorldMapSocialMarker[])method.Invoke(null, [self, new[] { map }])!;
            var markers = Build();
            Check(markers.Length == 3 && markers.Any(m => m.PlayerId == stranger.Id && m.Guild) && markers.Any(m => m.PlayerId == friend.Id && !m.Party) &&
                markers.Any(m => m.PlayerId == member.Id && m.Party), "Only eligible friends and party; no duplicates");
            Check(markers.All(m => m.X == 12 && m.Y == 17 && m.MapId == map), "Authoritative positions");
            online.TryRemove(friend.Id, out _);
            Check(Build().Length == 2, "Disconnected friend must disappear while guild member remains");
            self.Party = []; self.CachedFriends.Clear();
            self.GuildId = null;
            Check(Build().Length == 0, "Removed friends / party must disappear");

            var legend = new WorldMapLegend { Entries = [new() { AnimationId = Guid.NewGuid(), Label = " Potion shop " }] };
            Check(WorldMapLegend.FromJson(legend.ToJson()).Entries[0].Label == "Potion shop", "Legend JSON round-trip and trimming");
            foreach (var invalid in new[] { "null", "{\"Entries\":null}", "{\"Entries\":[{}]}" })
            {
                var rejected = false;
                try { WorldMapLegend.FromJson(invalid); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Invalid legend must be rejected");
            }
            legend.Entries = Enumerable.Range(0, 101).Select(_ => new WorldMapLegendEntry
                { AnimationId = Guid.NewGuid(), Label = "Shop" }).ToList();
            var tooLarge = false;
            try { WorldMapLegend.FromJson(legend.ToJson()); } catch (InvalidDataException) { tooLarge = true; }
            Check(tooLarge, "Oversized legend must be rejected");
            TestLegendPersistence();
            System.Console.WriteLine("PASS World Map server: friends/party, deduplication, authoritative locations, instance/grid/hidden filtering, logout/removal and legend validation");
        }
        finally { foreach (var player in spawned) online.TryRemove(player.Id, out _); }
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("World Map server: " + message); }

    private static void TestLegendPersistence()
    {
        var store = typeof(PacketSender).Assembly.GetType("Intersect.Server.Maps.WorldMapLegendStore", true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var cache = store.GetField("_json", flags)!;
        var oldCache = cache.GetValue(null);
        var oldDirectory = Environment.CurrentDirectory;
        var temporary = Directory.CreateTempSubdirectory("CR2026-worldmap-test-");
        var animation = new AnimationDescriptor(Guid.NewGuid()) { Name = "Legend fixture" };
        AnimationDescriptor.Lookup[animation.Id] = animation;
        try
        {
            Environment.CurrentDirectory = temporary.FullName;
            var legend = new WorldMapLegend { Entries = [new() { AnimationId = animation.Id, Label = "Potion shop" }] };
            var save = store.GetMethod("Save", flags)!;
            save.Invoke(null, [legend.ToJson()]);
            var persisted = File.ReadAllText(Path.Combine("resources", "worldmap-legend.json"));
            Check(WorldMapLegend.FromJson(persisted).Entries[0].AnimationId == animation.Id, "Legend must be persisted on disk");
            cache.SetValue(null, null);
            Check((string)store.GetProperty("Json", flags)!.GetValue(null)! == persisted, "Legend must survive a store reload");
            legend.Entries[0].AnimationId = Guid.NewGuid();
            var rejected = false;
            try { save.Invoke(null, [legend.ToJson()]); }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidDataException) { rejected = true; }
            Check(rejected && File.ReadAllText(Path.Combine("resources", "worldmap-legend.json")) == persisted,
                "Missing animations must be rejected without overwriting the saved legend");
        }
        finally
        {
            Environment.CurrentDirectory = oldDirectory;
            cache.SetValue(null, oldCache);
            AnimationDescriptor.Lookup.DeleteAt(animation.Id);
            var file = Path.Combine(temporary.FullName, "resources", "worldmap-legend.json");
            if (File.Exists(file)) File.Delete(file);
            var resources = Path.Combine(temporary.FullName, "resources");
            if (Directory.Exists(resources)) Directory.Delete(resources);
            temporary.Delete();
        }
    }
}
