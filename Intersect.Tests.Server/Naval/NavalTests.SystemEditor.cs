using Intersect.Framework.Core.Naval;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Naval;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [Test]
    public void EditorSaveRejectsAStaleVersionAndPreservesUnknownData()
    {
        using var files = new NavalSetupFiles();
        File.WriteAllText(files.Path, "{\"Enabled\":false,\"Ships\":[],\"FutureSetting\":{\"keep\":7}}");
        var store = new NavalConfigurationStore(files.Path);
        var edited = store.Load(); var version = NavalConfigurationStore.Version(edited.SourceJson);
        edited.Enabled = true; edited.LaunchSearchRadius = 6;
        store.Replace(edited, version);
        Assert.That(store.Load().LaunchSearchRadius, Is.EqualTo(6));
        Assert.That(File.ReadAllText(files.Path), Does.Contain("FutureSetting"));
        var saved = File.ReadAllText(files.Path);
        Assert.Throws<IOException>(() => store.Replace(edited, version));
        Assert.That(File.ReadAllText(files.Path), Is.EqualTo(saved));
        Assert.That(Directory.GetFiles(files.Directory, "*.tmp"), Is.Empty);
    }

    [Test]
    public void InvalidEditorValuesAndOversizedUtf8NeverReplaceThePreviousFile()
    {
        using var files = new NavalSetupFiles();
        File.WriteAllText(files.Path, "{\"Ships\":[]}");
        var store = new NavalConfigurationStore(files.Path); var edited = store.Load();
        var version = NavalConfigurationStore.Version(edited.SourceJson);
        edited.ShopInteractionRange = double.NaN;
        Assert.Throws<InvalidDataException>(() => store.Replace(edited, version));
        Assert.Throws<InvalidDataException>(() => NavalConfigurationStore.WriteAtomic(files.Path, new string('é', 100), 199));
        Assert.That(File.ReadAllText(files.Path), Is.EqualTo("{\"Ships\":[]}"));
    }

    [Test]
    public void FleetAndNpcShopRejectEachOthersOperationsAndForgedViews()
    {
        foreach (var action in Enum.GetValues<HarbourAction>())
        {
            Assert.That(HarbourAccessRules.Allows(HarbourView.Fleet, action),
                Is.EqualTo(action is HarbourAction.Open or HarbourAction.Launch or HarbourAction.Rename or HarbourAction.ClaimCargo or HarbourAction.DiscardCargo));
            Assert.That(HarbourAccessRules.Allows(HarbourView.Shop, action),
                Is.EqualTo(action is HarbourAction.Open or HarbourAction.Buy or HarbourAction.Repair));
        }
        Assert.That(HarbourAccessRules.Allows((HarbourView)255, HarbourAction.Open), Is.False);
        Assert.That(HarbourAccessRules.Allows(HarbourView.Shop, (HarbourAction)255), Is.False);
    }

    [Test]
    public void NpcShopSessionExpiresAndCannotBeUsedOnAnotherMapOrFarFromTheNpc()
    {
        Assert.That(HarbourAccessRules.ShopSessionValid(Map, 12, 10, Map, 10, 10, 100, 200, 4), Is.True);
        Assert.That(HarbourAccessRules.ShopSessionValid(Map, 12, 10, Map, 10, 10, 200, 200, 4), Is.False);
        Assert.That(HarbourAccessRules.ShopSessionValid(Guid.NewGuid(), 12, 10, Map, 10, 10, 100, 200, 4), Is.False);
        Assert.That(HarbourAccessRules.ShopSessionValid(Map, 15, 10, Map, 10, 10, 100, 200, 4), Is.False);
        Assert.That(HarbourAccessRules.ShopSessionValid(Map, 10, 10, Guid.Empty, 10, 10, 100, 200, 4), Is.False);
    }

    [Test]
    public void NewEditorAndHarbourFieldsRoundTripWithoutChangingExistingCommandIds()
    {
        EditorPacket request = new NavalConfigurationRequestPacket { Section = 2, ExpectedVersion = "version", ConfigurationJson = "{}" };
        var roundTrip = MessagePackSerializer.Deserialize<EditorPacket>(MessagePackSerializer.Serialize(request));
        Assert.That(roundTrip, Is.TypeOf<NavalConfigurationRequestPacket>());
        Assert.That(((NavalConfigurationRequestPacket)roundTrip).RequestId, Is.EqualTo(((NavalConfigurationRequestPacket)request).RequestId));
        var shop = new HarbourStatePacket { View = HarbourView.Shop, Entries = [new() { ModelId = "royal", ForSale = false }] };
        var restored = MessagePackSerializer.Deserialize<HarbourStatePacket>(MessagePackSerializer.Serialize(shop));
        Assert.That(restored.View, Is.EqualTo(HarbourView.Shop));
        Assert.That(restored.Entries.Single().ForSale, Is.False);
        Assert.That(new HarbourRequestPacket().View, Is.EqualTo(HarbourView.Fleet));
        Assert.That((int)EventCommandType.OpenRoyalStylist, Is.EqualTo(1040));
        Assert.That((int)new OpenNavalShopCommand().Type, Is.EqualTo(1050));
        Assert.That((int)new OpenNavalAmmoShopCommand().Type,Is.EqualTo(1051));
        Assert.That(JsonConvert.DeserializeObject<OpenNavalAmmoShopCommand>(JsonConvert.SerializeObject(new OpenNavalAmmoShopCommand()))!.Type,Is.EqualTo(EventCommandType.OpenNavalAmmoShop));
        var command = JsonConvert.DeserializeObject<OpenNavalShopCommand>(JsonConvert.SerializeObject(new OpenNavalShopCommand()));
        Assert.That(command!.Type, Is.EqualTo(EventCommandType.OpenNavalShop));
    }

    [Test]
    public void AllNavalEditorCaptionsHaveCompatibleEnglishFrenchAndSpanishEntries()
    {
        foreach (var entry in NavalSetupText.Catalog.Entries)
            foreach (var language in new[] { "en", "fr", "es" })
            {
                Assert.That(entry.Value.ContainsKey(language), Is.True, entry.Key);
                Assert.That(Intersect.Localization.GameTextTokens.Compatible(entry.Key, entry.Value[language]), Is.True, entry.Key);
            }
    }
}
