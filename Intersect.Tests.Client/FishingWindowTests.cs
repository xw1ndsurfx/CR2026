using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.Core;
using Intersect.Client.Core.Sounds;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Input;
using Intersect.Client.Framework.Input;
using Intersect.Client.Framework.Network;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Client.Interface.Game.Chat;
using Intersect.Client.Networking;
using Intersect.Enums;
using Intersect.Framework.Core.Fishing;
using Intersect.Network;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ClientInterface = Intersect.Client.Interface.Interface;
using ClientNetwork = Intersect.Client.Networking.Network;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Tests.Client;

/// <summary>
/// Exercises the real packet registry, transport serialization, game UI callback,
/// native fishing controls and close/reopen lifecycle without a server or GPU.
/// </summary>
[TestFixture, NonParallelizable]
public sealed class FishingWindowTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

    [TestCase(1600, 900)]
    [TestCase(800, 600)]
    public void ServerOpenPacketCreatesVisibleInteractiveWindowAndReopensAfterClosing(int width, int height)
    {
        using var fixture = new ClientFixture(width, height);
        var configuration = Configuration();
        var state = Ready(configuration);
        var opening = OpenPacket(configuration, state);

        fixture.Receive(opening);
        var window = fixture.Window;
        Assert.That(window, Is.Not.Null, "The registered client handler must construct the native fishing window.");
        Assert.That(window!.SessionId, Is.EqualTo(state.SessionId));
        Assert.That(window.IsVisibleInTree, Is.True);
        Assert.That(ClientInterface.InputBlockingComponents, Does.Contain(window));
        Assert.That(window.Width, Is.EqualTo(width));
        Assert.That(window.Height, Is.EqualTo(height));

        var cast = Descendants(window).OfType<Button>().Single(button => button.Name == "FishingCast");
        Assert.That(cast.IsVisibleInTree && !cast.IsDisabled, Is.True);
        Assert.That(cast.X, Is.GreaterThanOrEqualTo(0));
        Assert.That(cast.Right, Is.LessThanOrEqualTo(width));
        Assert.That(cast.Bottom, Is.LessThanOrEqualTo(height));
        Assert.That(window.GetComponentAt(cast.X + cast.Width / 2, cast.Y + cast.Height / 2), Is.SameAs(cast),
            "The fishing viewport must not intercept the cast button.");

        // Exercise production drawing with absent optional textures: controls and
        // the fallback water scene must still render rather than hiding the UI.
        typeof(FishingWindow).GetMethod("Render", Hidden)!.Invoke(window, [fixture.Skin]);
        Assert.That(fixture.Renderer.Fills, Is.Not.Empty);
        Assert.That(fixture.Content.Requests, Does.Contain((TextureType.Gui, "fishing/gui/fishing_shell.png")));

        // Button.Press only invokes the visual hook; native Clicked is emitted
        // by the hovered control receiving a complete press/release pair.
        InputHandler.HoveredControl = cast;
        var mouseInput = typeof(ControlBase).GetMethod("InputMouseButtonState", Hidden)!;
        var position = cast.ToCanvas(cast.Width / 2, cast.Height / 2);
        mouseInput.Invoke(cast, [MouseButton.Left, position, true, true]);
        mouseInput.Invoke(cast, [MouseButton.Left, position, false, true]);
        var request = fixture.Socket.Packets.OfType<FishingRequestPacket>().Single();
        Assert.That(request.Action, Is.EqualTo(FishingAction.Cast));
        Assert.That(request.SessionId, Is.EqualTo(state.SessionId));
        Assert.That(request.Sequence, Is.EqualTo(1));

        state.Phase = FishingPhase.Closed;
        state.Revision++;
        fixture.Receive(new FishingStatePacket { StateJson = state.ToJson() });
        fixture.UpdateFishing();
        Assert.That(fixture.Window, Is.Null);
        Assert.That(ClientInterface.InputBlockingComponents, Does.Not.Contain(window));
        Assert.That(fixture.Canvas.Children, Does.Not.Contain(window));
        Assert.That(fixture.Socket.Packets.OfType<FishingRequestPacket>().Last().Action, Is.EqualTo(FishingAction.Leave));

        // A delayed initial packet from the closed session must not resurrect it.
        fixture.Receive(opening);
        Assert.That(fixture.Window, Is.Null);
        var next = Ready(configuration);
        fixture.Receive(OpenPacket(configuration, next));
        Assert.That(fixture.Window?.SessionId, Is.EqualTo(next.SessionId));
        Assert.That(fixture.Window?.IsVisibleInTree, Is.True);
    }

    [Test]
    public void OpeningRefusalIsVisibleInChatWithoutCreatingAnEmptyWindow()
    {
        using var fixture = new ClientFixture(1600, 900);
        const string message = "Equip a compatible fishing tool.";
        fixture.Receive(new FishingStatePacket { Message = message });
        Assert.That(fixture.Window, Is.Null);
        Assert.That(ChatboxMsg.GetMessages().Last().Message, Is.EqualTo(message));
        Assert.That(ChatboxMsg.GetMessages().Last().Type, Is.EqualTo(ChatMessageType.Error));
    }

    private static FishingConfiguration Configuration()
    {
        var tool = new FishingToolDefinition { ItemId = Guid.NewGuid(), Type = FishingToolType.Net, WorldProfile = FishingWorldProfile.Net() };
        var zone = new FishingZoneDefinition
        {
            Id = Guid.NewGuid(), Name = "Fishing opening fixture", Enabled = true,
            MapIds = [Guid.NewGuid()], AllowAutomatic = true,
        };
        return new FishingConfiguration { Tools = [tool], Zones = [zone] };
    }

    private static FishingState Ready(FishingConfiguration configuration) => new()
    {
        SessionId = Guid.NewGuid(), ZoneId = configuration.Zones[0].Id, ZoneName = configuration.Zones[0].Name,
        ToolItemId = configuration.Tools[0].ItemId, ToolType = FishingToolType.Net,
        Phase = FishingPhase.Ready, AllowAutomatic = true, Level = 1, ExperienceRequired = 100,
        Revision = 1, SoundCue = "session_start", SoundSequence = 1,
    };

    private static FishingStatePacket OpenPacket(FishingConfiguration configuration, FishingState state) => new()
    {
        Open = true, StateJson = state.ToJson(), ConfigurationJson = configuration.ToJson(),
    };

    private static IEnumerable<ControlBase> Descendants(ControlBase parent)
    {
        foreach (var child in parent.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class ClientFixture : IDisposable
    {
        private readonly object? _previousContent = Field(typeof(GameContentManager), "_current").GetValue(null);
        private readonly GameContentManager? _previousGlobalContent = Globals.ContentManager;
        private readonly object? _previousSocket = Field(typeof(ClientNetwork), "_socket").GetValue(null);
        private readonly object? _previousUi = Field(typeof(ClientInterface), "_uiInGame").GetValue(null);
        private readonly Player? _previousPlayer = Globals.Me;
        private readonly ControlBase? _previousKeyboardFocus = InputHandler.KeyboardFocus;
        private readonly ControlBase? _previousMouseFocus = InputHandler.MouseFocus;
        private readonly ControlBase? _previousHoveredControl = InputHandler.HoveredControl;
        private readonly ChatboxMsg[] _previousMessages = ChatboxMsg.GetMessages().ToArray();
        private readonly List<Sound> _sounds = (List<Sound>)Field(typeof(Audio), "sGameSounds").GetValue(null)!;
        private readonly Sound[] _previousSounds;
        private readonly Dictionary<Type, ushort> _previousTypes = new(PackedIntersectPacket.KnownTypes);
        private readonly Dictionary<ushort, Type> _previousKeys = new(PackedIntersectPacket.KnownKeys);
        private readonly byte[] _previousFingerprint = PackedIntersectPacket.CurrentProtocolFingerprint;
        private readonly PacketHandlerRegistry _handlers;
        private readonly PacketHandler _handler;
        private readonly GameInterface _game;

        internal readonly TestContent Content = new();
        internal readonly RecordingRenderer Renderer = new();
        internal readonly RecordingSocket Socket = new();
        internal readonly TestSkin Skin;
        internal readonly Canvas Canvas;
        internal FishingWindow? Window => (FishingWindow?)Field(typeof(GameInterface), "_fishingWindow").GetValue(_game);

        internal ClientFixture(int width, int height)
        {
            Skin = new(Renderer);
            Canvas = new(Skin) { Size = new Point(width, height) };
            // Isolate the fishing surface from unrelated main HUD constructors;
            // the actual ApplyFishingState callback and FishingWindow are used.
            _game = (GameInterface)RuntimeHelpers.GetUninitializedObject(typeof(GameInterface));
            Field(typeof(GameInterface), "<GameCanvas>k__BackingField").SetValue(_game, Canvas);
            Field(typeof(ClientInterface), "_uiInGame").SetValue(null, _game);
            var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
            GC.SuppressFinalize(player);
            player.Id = Guid.NewGuid();
            Globals.Me = player;
            Globals.ContentManager = Content;
            ClientNetwork.Socket = Socket;
            ChatboxMsg.ClearMessages();
            _previousSounds = _sounds.ToArray();
            _sounds.Clear();

            var types = new PacketTypeRegistry(NullLogger.Instance, typeof(FishingStatePacket).Assembly);
            Assert.That(types.TryRegisterBuiltIn(), Is.True);
            PackedIntersectPacket.AddKnownTypes(types.BuiltInTypes);
            _handlers = new(types, NullLogger.Instance);
            var context = DispatchProxy.Create<IClientContext, ClientContextProxy>();
            _handler = new(context, _handlers);
            Assert.That(_handlers.HasHandler<FishingStatePacket>(), Is.True);
            Assert.That(_handlers.HasHandler<FishingWorldPacket>(), Is.True);
        }

        internal void Receive(FishingStatePacket packet)
        {
            var payload = MessagePacker.Instance.Serialize(packet);
            var decoded = MessagePacker.Instance.Deserialize(payload, out var failure);
            Assert.That(failure, Is.EqualTo(PacketDecodeFailure.None));
            Assert.That(decoded, Is.TypeOf<FishingStatePacket>());
            Assert.That(_handler.HandlePacket((FishingStatePacket)decoded!), Is.True);
        }

        internal void UpdateFishing() => typeof(GameInterface).GetMethod("UpdateFishing", Hidden)!.Invoke(_game, null);

        public void Dispose()
        {
            typeof(GameInterface).GetMethod("DisposeFishing", Hidden)!.Invoke(_game, null);
            Canvas.Dispose();
            Skin.Dispose();
            Renderer.Dispose();
            _handlers.Dispose();
            Globals.Me = _previousPlayer;
            Globals.ContentManager = _previousGlobalContent;
            Field(typeof(GameContentManager), "_current").SetValue(null, _previousContent);
            Field(typeof(ClientNetwork), "_socket").SetValue(null, _previousSocket);
            Field(typeof(ClientInterface), "_uiInGame").SetValue(null, _previousUi);
            InputHandler.KeyboardFocus = _previousKeyboardFocus;
            InputHandler.MouseFocus = _previousMouseFocus;
            InputHandler.HoveredControl = _previousHoveredControl;
            ChatboxMsg.ClearMessages();
            foreach (var message in _previousMessages) ChatboxMsg.AddMessage(message);
            _sounds.Clear();
            _sounds.AddRange(_previousSounds);
            PackedIntersectPacket.KnownTypes.Clear();
            foreach (var pair in _previousTypes) PackedIntersectPacket.KnownTypes.Add(pair.Key, pair.Value);
            PackedIntersectPacket.KnownKeys.Clear();
            foreach (var pair in _previousKeys) PackedIntersectPacket.KnownKeys.Add(pair.Key, pair.Value);
            Field(typeof(PackedIntersectPacket), "<CurrentProtocolFingerprint>k__BackingField").SetValue(null, _previousFingerprint);
        }

        private static FieldInfo Field(Type type, string name) => type.GetField(name, Hidden)!;
    }

    public class ClientContextProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "get_Logger"
            ? NullLogger.Instance
            : method.ReturnType == typeof(void) || !method.ReturnType.IsValueType ? null : Activator.CreateInstance(method.ReturnType);
    }

    private sealed class RecordingSocket : GameSocket
    {
        internal readonly List<object> Packets = [];
        public override void SendPacket(object packet) => Packets.Add(packet);
        public override bool IsConnected => true;
        public override int Ping => 0;
        public override INetwork Network => null!;
        public override void Connect(string host, int port) { }
        public override void Update() { }
        public override void Disconnect(string reason) { }
        public override void Dispose() { }
    }

    private sealed class TestContent : GameContentManager
    {
        internal readonly List<(TextureType, string)> Requests = [];
        internal TestContent()
        {
            mFontDict["sourcesanspro"] = new TestFont();
            mFontDict["sourcesansproblack"] = new TestFont();
        }
        public override IGameTexture? GetTexture(TextureType type, string name) { Requests.Add((type, name)); return null; }
        public override void LoadTilesets(string[] names) => throw new InvalidOperationException();
        public override void LoadTexturePacks() => throw new InvalidOperationException();
        public override void LoadItems() => throw new InvalidOperationException();
        public override void LoadEntities() => throw new InvalidOperationException();
        public override void LoadSpells() => throw new InvalidOperationException();
        public override void LoadAnimations() => throw new InvalidOperationException();
        public override void LoadFaces() => throw new InvalidOperationException();
        public override void LoadImages() => throw new InvalidOperationException();
        public override void LoadDungeons() => throw new InvalidOperationException();
        public override void LoadFogs() => throw new InvalidOperationException();
        public override void LoadResources() => throw new InvalidOperationException();
        public override void LoadPaperdolls() => throw new InvalidOperationException();
        public override void LoadGui() => throw new InvalidOperationException();
        public override void LoadMisc() => throw new InvalidOperationException();
        public override void LoadFonts() => throw new InvalidOperationException();
        public override void LoadShaders() => throw new InvalidOperationException();
        public override void LoadSounds() => throw new InvalidOperationException();
        public override void LoadMusic() => throw new InvalidOperationException();
        protected override TAsset Load<TAsset>(Dictionary<string, IAsset> lookup, ContentType type, string name, Func<Stream> stream) =>
            throw new InvalidOperationException();
    }

    private sealed class TestSkin : SkinBase
    {
        internal TestSkin(RendererBase renderer) : base(renderer)
        {
            DefaultFont = new TestFont(); DefaultFontSize = 12;
            Colors.Label.Normal = Colors.Label.Disabled = Color.White;
            Colors.Button.Normal = Colors.Button.Hover = Colors.Button.Active = Colors.Button.Disabled = Color.White;
        }
    }

    private sealed class RecordingRenderer : RendererBase
    {
        internal readonly List<Rectangle> Fills = [];
        public override void DrawFilledRect(Rectangle rectangle) => Fills.Add(rectangle);
        public override Point MeasureText(IFont? font, int size, string? text, float scale = 1f) =>
            new((int)Math.Ceiling((text?.Length ?? 0) * size * .6 * scale), (int)Math.Ceiling((size + 3) * scale));
    }

    private sealed class TestFont : IFont
    {
        public string Name => "fishing-open-test";
        public ICollection<int> SupportedSizes { get; } = [10, 12, 18, 22, 29];
        public int PickBestMatchFor(int size) => size;
        public int GetNextFontSize(int startSize, int direction, int limit = 0) => startSize + direction;
    }
}
