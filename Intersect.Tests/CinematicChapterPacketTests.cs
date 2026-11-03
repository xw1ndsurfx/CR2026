using Intersect.Enums;
using Intersect.Network;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using MessagePack;
using MessagePack.Resolvers;
using NUnit.Framework;

namespace Intersect.Tests;

[TestFixture]
public sealed class CinematicChapterPacketTests
{
    private static readonly MessagePackSerializerOptions WireOptions = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create(NativeGuidResolver.Instance, StandardResolver.Instance));

    [MessagePackObject]
    public sealed class HistoricalCreate
    {
        [Key(0)] public GameObjectType Type { get; set; }
    }

    [MessagePackObject]
    public sealed class HistoricalObject
    {
        [Key(0)] public Guid Id { get; set; }
        [Key(1)] public GameObjectType Type { get; set; }
        [Key(2)] public bool AnotherFollowing { get; set; }
        [Key(3)] public bool Deleted { get; set; }
        [Key(4)] public string Data { get; set; } = "";
    }

    [Test]
    public void HistoricalCreationPayloadStillRequestsAServerAssignedIdentity()
    {
        var bytes = MessagePackSerializer.Serialize(new HistoricalCreate { Type = GameObjectType.Event }, WireOptions);
        var received = MessagePackSerializer.Deserialize<CreateGameObjectPacket>(bytes, WireOptions);
        Assert.That(received.Type, Is.EqualTo(GameObjectType.Event));
        Assert.That(received.RequestedId, Is.EqualTo(Guid.Empty));
        Assert.That(new CreateGameObjectPacket(GameObjectType.PlayerVariable).RequestedId, Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void HistoricalObjectPayloadRemainsASuccessfulObjectUpdate()
    {
        var old = new HistoricalObject { Id = Guid.NewGuid(), Type = GameObjectType.PlayerVariable, Data = "{\"Name\":\"Existing\"}" };
        var received = MessagePackSerializer.Deserialize<GameObjectPacket>(MessagePackSerializer.Serialize(old, WireOptions), WireOptions);
        Assert.That(received.Id, Is.EqualTo(old.Id));
        Assert.That(received.Data, Is.EqualTo(old.Data));
        Assert.That(received.CreationError, Is.Null.Or.Empty);
        Assert.That(received.CreationSucceeded, Is.False);
    }

    [Test]
    public void RequestedIdentityAndConflictReceiptRoundTripThroughNativePacketRegistry()
    {
        Options.EnsureCreated();
        var registry = new PacketTypeRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, typeof(IntersectPacket).Assembly);
        Assert.That(registry.TryRegisterBuiltIn(), Is.True);
        PackedIntersectPacket.AddKnownTypes(registry.Types);
        var request = new CreateGameObjectPacket(GameObjectType.Event, Guid.NewGuid());
        var created = MessagePacker.Instance.Deserialize<CreateGameObjectPacket>(MessagePacker.Instance.Serialize(request));
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.RequestedId, Is.EqualTo(request.RequestedId));
        var error = new GameObjectPacket(request.RequestedId, request.Type, "", false, false) { CreationError = "ChapterObjectConflict" };
        var result = MessagePacker.Instance.Deserialize<GameObjectPacket>(MessagePacker.Instance.Serialize(error));
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(request.RequestedId));
        Assert.That(result.CreationError, Is.EqualTo("ChapterObjectConflict"));
        Assert.That(result.Deleted, Is.False);
        var success = new GameObjectPacket(request.RequestedId, request.Type, "{}", false, false) { CreationSucceeded = true };
        var successResult = MessagePacker.Instance.Deserialize<GameObjectPacket>(MessagePacker.Instance.Serialize(success));
        Assert.That(successResult, Is.Not.Null);
        Assert.That(successResult!.CreationSucceeded, Is.True);
        Assert.That(successResult.Id, Is.EqualTo(request.RequestedId));
    }
}
