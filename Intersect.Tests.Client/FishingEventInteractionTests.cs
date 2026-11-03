using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Items;
using Intersect.Client.Framework.Network;
using Intersect.Client.General;
using Intersect.Client.Items;
using Intersect.Client.Maps;
using Intersect.Enums;
using Intersect.Framework.Core.Fishing;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network;
using Intersect.Network.Packets.Client;
using NUnit.Framework;
using ClientEvent = Intersect.Client.Entities.Events.Event;
using ClientNetwork = Intersect.Client.Networking.Network;

namespace Intersect.Tests.Client;

[TestFixture, NonParallelizable]
public sealed class FishingEventInteractionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void EventWithoutGraphicAndWithInspectorDisabledStillActivates(bool equipNet)
    {
        using var scene = new InteractionScene(equipNet);
        var fishingEvent = scene.AddEvent();

        Assert.Multiple(() =>
        {
            Assert.That(fishingEvent.Graphic.Type, Is.EqualTo(EventGraphicType.None));
            Assert.That(fishingEvent.DisablePreview, Is.True);
            if (equipNet)
            {
                Assert.That(scene.Net.Type, Is.EqualTo(FishingToolType.Net));
                Assert.That(scene.Player.MyEquipment[Options.Instance.Equipment.WeaponSlot], Is.Zero);
                Assert.That(scene.Player.Inventory[0].ItemId, Is.EqualTo(scene.Net.ItemId));
                Assert.That(scene.Player.Inventory[0].Descriptor.ItemType, Is.EqualTo(ItemType.Equipment));
            }
        });

        Assert.That(scene.Player.TryAttack(), Is.True);
        AssertActivation(scene, fishingEvent);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AuthoredActionEventOnResourceTileTakesPriorityOverHarvesting(bool equipNet)
    {
        using var scene = new InteractionScene(equipNet);
        scene.AddResource();
        var fishingEvent = scene.AddEvent();

        Assert.That(scene.Player.TryAttack(), Is.True);

        // The old resource-first path sends an AttackPacket, which asks the server for an axe
        // and never executes even the first Show Text command in the authored fishing event.
        AssertActivation(scene, fishingEvent);
    }

    [Test]
    public void ResourceWithoutAnActionEventStillUsesHarvesting()
    {
        using var scene = new InteractionScene(equipNet: true);
        var resource = scene.AddResource();

        Assert.That(scene.Player.TryAttack(), Is.True);
        AssertAttack(scene, resource);
    }

    [Test]
    public void ActionEventOnNeighboringTileDoesNotInterceptResourceInteraction()
    {
        using var scene = new InteractionScene(equipNet: true);
        var resource = scene.AddResource();
        scene.AddEvent(x: 11, y: 11);

        Assert.That(scene.Player.TryAttack(), Is.True);
        AssertAttack(scene, resource);
    }

    [TestCase(EventTrigger.PlayerCollide)]
    [TestCase(EventTrigger.PlayerBump)]
    [TestCase(EventTrigger.Autorun)]
    public void OtherEventTriggersDoNotReplaceResourceHarvesting(EventTrigger trigger)
    {
        using var scene = new InteractionScene(equipNet: true);
        var resource = scene.AddResource();
        scene.AddEvent(trigger: trigger);

        Assert.That(scene.Player.TryAttack(), Is.True);
        AssertAttack(scene, resource);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AttackableNpcKeepsCombatPriority(bool eventOnSameTile)
    {
        using var scene = new InteractionScene(equipNet: true);
        var npc = scene.AddNpc();
        if (eventOnSameTile) scene.AddEvent();

        Assert.That(scene.Player.TryAttack(), Is.True);
        AssertAttack(scene, npc);
    }

    [Test]
    public void ResourceAndEventDoNotHideAnAttackableNpcOnTheSameTile()
    {
        using var scene = new InteractionScene(equipNet: true);
        scene.AddResource();
        var npc = scene.AddNpc();
        scene.AddEvent();

        Assert.That(scene.Player.TryAttack(), Is.True);
        AssertAttack(scene, npc);
    }

    private static void AssertActivation(InteractionScene scene, ClientEvent expected)
    {
        Assert.That(scene.Socket.Packets, Has.Count.EqualTo(1));
        Assert.That(scene.Socket.Packets[0], Is.TypeOf<ActivateEventPacket>(),
            "The authored event must be activated instead of sending an ordinary resource attack.");
        Assert.That(((ActivateEventPacket)scene.Socket.Packets[0]).EventId, Is.EqualTo(expected.Id));
    }

    private static void AssertAttack(InteractionScene scene, Entity expected)
    {
        Assert.That(scene.Socket.Packets, Has.Count.EqualTo(1));
        Assert.That(scene.Socket.Packets[0], Is.TypeOf<AttackPacket>());
        Assert.That(((AttackPacket)scene.Socket.Packets[0]).Target, Is.EqualTo(expected.Id));
    }

    private sealed class InteractionScene : IDisposable
    {
        private static readonly BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly PropertyInfo OptionsProperty = typeof(Options).GetProperty(nameof(Options.Instance))!;
        private static readonly FieldInfo SocketField = typeof(ClientNetwork).GetField("_socket", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly Options _previousOptions = Options.Instance;
        private readonly Player? _previousPlayer = Globals.Me;
        private readonly Guid[,]? _previousGrid = Globals.MapGrid;
        private readonly long _previousGridWidth = Globals.MapGridWidth;
        private readonly long _previousGridHeight = Globals.MapGridHeight;
        private readonly object? _previousSocket = SocketField.GetValue(null);
        private readonly List<Entity> _globalEntities = [];
        private readonly ItemDescriptor _netItem;
        private readonly MapInstance _map;

        public InteractionScene(bool equipNet)
        {
            var options = new Options();
            // The normal configuration deserializer invokes this setter; a bare Options instance
            // leaves the derived MovementDirections at zero and cannot accept a facing direction.
            options.Map.EnableDiagonalMovement = true;
            OptionsProperty.SetValue(null, options);
            _map = new MapInstance(Guid.NewGuid());
            GC.SuppressFinalize(_map);
            MapInstance.Lookup.Set(_map.Id, _map);
            Globals.MapGrid = new Guid[1, 1];
            Globals.MapGrid[0, 0] = _map.Id;
            Globals.MapGridWidth = Globals.MapGridHeight = 1;

            Player = NewEntity<Player>(EntityType.Player, 10, 10);
            Player.DirectionFacing = Direction.Down;
            Player.Stat = new int[Enum.GetValues<Stat>().Length];
            Player.MyEquipment = Enumerable.Repeat(-1, Options.Instance.Equipment.Slots.Count).ToArray();
            SetEntityField(Player, "_equipment", new Guid[Options.Instance.Equipment.Slots.Count]);
            SetEntityField(Player, "<Inventory>k__BackingField",
                Enumerable.Range(0, Options.Instance.Player.MaxInventory).Select(_ => (IItem)new Item()).ToArray());

            Options.Instance.Equipment.ToolTypes.Add("Fishing Net");
            _netItem = new ItemDescriptor(Guid.NewGuid())
            {
                Name = "Fishing Net",
                ItemType = ItemType.Equipment,
                EquipmentSlot = Options.Instance.Equipment.WeaponSlot,
                Tool = Options.Instance.Equipment.ToolTypes.Count - 1,
                AttackSpeedModifier = 1,
                AttackSpeedValue = 400,
            };
            ItemDescriptor.Lookup.Set(_netItem.Id, _netItem);
            Net = new FishingToolDefinition { ItemId = _netItem.Id, Type = FishingToolType.Net };
            if (equipNet)
            {
                Player.Inventory[0].ItemId = Net.ItemId;
                Player.Inventory[0].Quantity = 1;
                Player.MyEquipment[_netItem.EquipmentSlot] = 0;
                Player.Equipment[_netItem.EquipmentSlot] = Net.ItemId;
            }

            Globals.Me = Player;
            AddGlobal(Player);
            ClientNetwork.Socket = Socket;
        }

        public Player Player { get; }
        public FishingToolDefinition Net { get; }
        public RecordingSocket Socket { get; } = new();

        public ClientEvent AddEvent(byte x = 10, byte y = 11, EventTrigger trigger = EventTrigger.ActionButton)
        {
            var result = NewEntity<ClientEvent>(EntityType.Event, x, y);
            result.Trigger = trigger;
            result.DisablePreview = true;
            result.Graphic = new EventGraphic { Type = EventGraphicType.None };
            _map.LocalEntities.Add(result.Id, result);
            return result;
        }

        public Resource AddResource()
        {
            var resource = NewEntity<Resource>(EntityType.Resource, 10, 11);
            resource.IsDead = false;
            AddGlobal(resource);
            return resource;
        }

        public Entity AddNpc()
        {
            // NPCs use the ordinary client Entity representation; its attackability must stay intact.
            var npc = NewEntity<Entity>(EntityType.GlobalEntity, 10, 11);
            AddGlobal(npc);
            return npc;
        }

        private T NewEntity<T>(EntityType type, byte x, byte y) where T : Entity
        {
            // Use the actual runtime types without starting their renderer/content-loading constructors.
            var entity = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
            GC.SuppressFinalize(entity);
            entity.Id = Guid.NewGuid();
            SetEntityField(entity, "<MapId>k__BackingField", _map.Id);
            SetEntityField(entity, "<Type>k__BackingField", type);
            SetEntityField(entity, "MapWidth", Options.Instance.Map.MapWidth);
            SetEntityField(entity, "MapHeight", Options.Instance.Map.MapHeight);
            SetEntityField(entity, "TileWidth", Options.Instance.Map.TileWidth);
            SetEntityField(entity, "TileHeight", Options.Instance.Map.TileHeight);
            entity.X = x;
            entity.Y = y;
            return entity;
        }

        private static void SetEntityField(Entity entity, string name, object value) =>
            typeof(Entity).GetField(name, HiddenInstance)!.SetValue(entity, value);

        private void AddGlobal(Entity entity)
        {
            _globalEntities.Add(entity);
            Globals.Entities.Add(entity.Id, entity);
        }

        public void Dispose()
        {
            foreach (var entity in _globalEntities) Globals.Entities.Remove(entity.Id);
            _map.LocalEntities.Clear();
            MapInstance.Lookup.Delete(_map);
            ItemDescriptor.Lookup.Delete(_netItem);
            Globals.Me = _previousPlayer;
            Globals.MapGrid = _previousGrid;
            Globals.MapGridWidth = _previousGridWidth;
            Globals.MapGridHeight = _previousGridHeight;
            SocketField.SetValue(null, _previousSocket);
            OptionsProperty.SetValue(null, _previousOptions);
        }
    }

    private sealed class RecordingSocket : GameSocket
    {
        public List<object> Packets { get; } = [];
        public override bool IsConnected => true;
        public override int Ping => 0;
        public override INetwork Network => null!;
        public override void SendPacket(object packet) => Packets.Add(packet);
        public override void Connect(string host, int port) { }
        public override void Update() { }
        public override void Disconnect(string reason) { }
        public override void Dispose() { }
    }
}
