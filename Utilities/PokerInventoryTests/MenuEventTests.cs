using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Network.Packets.ChatEmotes;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
internal static class MenuEventTests
{
    [ModuleInitializer] internal static void Run()
    {
        var previous = Intersect.Options.Instance;
        try
        {
            typeof(Intersect.Options).GetProperty(nameof(Intersect.Options.Instance))!.SetValue(null, new Intersect.Options());
            Verify();
        }
        catch (Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
        finally { typeof(Intersect.Options).GetProperty(nameof(Intersect.Options.Instance))!.SetValue(null, previous); }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Verify()
    {
        var player = new Player { InGame = true }; GC.SuppressFinalize(player);
        var client = (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client)); client.Entity = player;
        var handlerType = typeof(Player).Assembly.GetType("Intersect.Server.Networking.PacketHandler", true)!;
        var handler = RuntimeHelpers.GetUninitializedObject(handlerType);
        var receive = handlerType.GetMethod("HandlePacket", [typeof(Client), typeof(ChatEmoteRequestPacket)])!;
        var queue = typeof(Player).GetField("_queueStartCommonEvent", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(player)!;
        int Count() => (int)queue.GetType().GetProperty("Count")!.GetValue(queue)!;
        var emote = new EventDescriptor(Guid.NewGuid(), true) { Pages = [new()] };
        var mapEvent = new EventDescriptor(Guid.NewGuid(), false) { Pages = [new() { ShowInChatEmoteMenu = true }] };
        EventDescriptor.Lookup[emote.Id] = emote; EventDescriptor.Lookup[mapEvent.Id] = mapEvent;
        void Request(Guid id) => receive.Invoke(handler, [client, new ChatEmoteRequestPacket { EventId = id }]);
        try
        {
            Request(emote.Id); Request(mapEvent.Id); Request(Guid.NewGuid());
            Check(Count() == 0, "Client cannot execute unlisted, map-only or nonexistent events");
            emote.Pages[0].ShowInChatEmoteMenu = true;
            client.IsEditor = true; Request(emote.Id); client.IsEditor = false;
            player.IsDead = true; Request(emote.Id); player.IsDead = false;
            player.InShop = new Intersect.GameObjects.ShopDescriptor(); Request(emote.Id); player.InShop = null;
            Check(Count() == 0, "Editor sessions, dead players and busy transactions cannot run menu events");
            Request(emote.Id); Check(Count() == 1, "An authorized emote queues the actual common event");
            Request(emote.Id); Check(Count() == 1, "Repeated emote packets are throttled before adding events");
        }
        finally { EventDescriptor.Lookup.DeleteAt(emote.Id); EventDescriptor.Lookup.DeleteAt(mapEvent.Id); }
        System.Console.WriteLine("PASS actual chat-emote packet authorization, busy/dead/editor guards and execution throttling");
    }
}
