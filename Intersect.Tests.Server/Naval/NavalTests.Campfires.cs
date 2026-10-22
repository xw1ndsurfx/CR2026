using System.Reflection;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Server;
using Intersect.Server.Campfires;
using Intersect.Server.Entities;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test]
    public void CampfireRecipesReadLegacyIngredientsAndPersistTheirPlacedResultWithoutAnItem()
    {
        var ingredient = new CraftingRecipeIngredient(Guid.NewGuid(), 3);
        var recipe = new CraftingRecipeDescriptor(Guid.NewGuid());
        var property = typeof(CraftingRecipeDescriptor).GetProperty(nameof(recipe.IngredientsJson))!;
        property.SetValue(recipe, JsonConvert.SerializeObject(new[] { ingredient }));
        Assert.That(recipe.Campfire, Is.Null);
        Assert.That(recipe.Ingredients.Single().Quantity, Is.EqualTo(3));
        recipe.Campfire = new() { FireAnimationId = Guid.NewGuid(), AshesAnimationId = Guid.NewGuid(), FireSeconds = 45, AshesSeconds = 12 };
        var copy = new CraftingRecipeDescriptor(Guid.NewGuid());
        property.SetValue(copy, recipe.IngredientsJson);
        Assert.That(copy.Campfire!.IsValid, Is.True);
        Assert.That(copy.Campfire.FireSeconds, Is.EqualTo(45));
        Assert.That(copy.Ingredients.Single().ItemId, Is.EqualTo(ingredient.ItemId));
        Assert.That(copy.ItemId, Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void CampfireWorldCycleChangesToAshesThenRemovesAndKeepsInstancesSeparate()
    {
        var fireAnimation = new AnimationDescriptor(Guid.NewGuid());
        var ashes = new AnimationDescriptor(Guid.NewGuid());
        AnimationDescriptor.Lookup.Set(fireAnimation.Id, fireAnimation); AnimationDescriptor.Lookup.Set(ashes.Id, ashes);
        var player = CargoPlayer(); player.Id = Guid.NewGuid(); player.MapId = Guid.NewGuid(); player.X = 8; player.Y = 9;
        var result = new CampfireRecipeResult { FireAnimationId = fireAnimation.Id, AshesAnimationId = ashes.Id, FireSeconds = 20, AshesSeconds = 10 };
        var before = DateTime.UtcNow;
        try
        {
            Assert.That(CampfireRuntime.Place(player, result), Is.True);
            Assert.That(CampfireRuntime.Place(player, result), Is.False, "One active fire per owner; repeated requests cannot duplicate it.");
            var state = CampfireRuntime.Snapshot(player, before.AddSeconds(1)).Single();
            Assert.That(state.AnimationId, Is.EqualTo(fireAnimation.Id)); Assert.That(state.Burning, Is.True);
            Assert.That(state.X, Is.EqualTo(8)); Assert.That(state.Y, Is.EqualTo(9));
            Assert.That(CampfireRuntime.CanCook(player, state.Id), Is.True);
            player.X = 11; Assert.That(CampfireRuntime.CanCook(player, state.Id), Is.False); player.X = 8;
            var second = CargoPlayer(); second.MapId = player.MapId; second.MapInstanceId = Guid.NewGuid();
            Assert.That(CampfireRuntime.Snapshot(second, before.AddSeconds(1)), Is.Empty);
            Assert.That(CampfireRuntime.CanCook(second, state.Id), Is.False);
            var crew = CargoPlayer(); crew.Id = Guid.NewGuid(); crew.MapId = player.MapId; crew.X = 9; crew.Y = 9;
            Assert.That(CampfireRuntime.CanCook(crew, state.Id), Is.True, "Other nearby players can use the fire.");
            var burnt = CampfireRuntime.Snapshot(player, before.AddSeconds(25)).Single();
            Assert.That(burnt.AnimationId, Is.EqualTo(ashes.Id)); Assert.That(burnt.Burning, Is.False);
            Assert.That(CampfireRuntime.Snapshot(player, before.AddSeconds(35)), Is.Empty);
            Assert.That(CampfireRuntime.CanCook(player, state.Id), Is.False);
        }
        finally
        {
            CampfireRuntime.Snapshot(player, before.AddHours(3));
            AnimationDescriptor.Lookup.Delete(fireAnimation); AnimationDescriptor.Lookup.Delete(ashes);
        }
    }

    [Test]
    public void CampfireCraftConsumesIngredientsAndCreatesAWorldFireInsteadOfAnInventoryItem()
    {
        var flames = new AnimationDescriptor(Guid.NewGuid()); var ashes = new AnimationDescriptor(Guid.NewGuid());
        var wood = new ItemDescriptor(Guid.NewGuid()) { Name = "Wood", Stackable = true, MaxInventoryStack = 99 };
        AnimationDescriptor.Lookup.Set(flames.Id, flames); AnimationDescriptor.Lookup.Set(ashes.Id, ashes); ItemDescriptor.Lookup.Set(wood.Id, wood);
        var recipe = new CraftingRecipeDescriptor(Guid.NewGuid()) { Name = "Campfire", Ingredients = [new(wood.Id, 2)],
            Campfire = new() { FireAnimationId = flames.Id, AshesAnimationId = ashes.Id }, FailureChance = 0 };
        CraftingRecipeDescriptor.Lookup.Set(recipe.Id, recipe);
        var player = CargoPlayer(); player.Id = Guid.NewGuid(); player.MapId = Guid.NewGuid(); player.EntityLock = new(); player.Quests = []; player.Equipment = Enumerable.Repeat(-1, Options.Instance.Equipment.Slots.Count).ToArray();
        player.Items = new(Options.Instance.Player.MaxInventory, Intersect.Server.Database.PlayerData.Players.InventorySlot.Create);
        player.Items.FillToCapacity(); player.Items[0].Set(new Intersect.Server.Database.Item(wood.Id, 4));
        player.OpenCraftingTableId = CampfireRuntime.TableId; player.CraftingState = new() { Id = recipe.Id, RemainingCount = 1 };
        try
        {
            Assert.That(player.ShouldCancelCrafting(), Is.False);
            player.CraftItem();
            Assert.That(player.Items[0].Quantity, Is.EqualTo(2));
            Assert.That(player.Items.Count(slot => slot.ItemId != Guid.Empty), Is.EqualTo(1));
            Assert.That(CampfireRuntime.Snapshot(player, DateTime.UtcNow), Has.Length.EqualTo(1));
            Assert.That(player.CraftingState.RemainingCount, Is.Zero);
        }
        finally
        {
            CampfireRuntime.Snapshot(player, DateTime.UtcNow.AddHours(3));
            CraftingRecipeDescriptor.Lookup.Delete(recipe); ItemDescriptor.Lookup.Delete(wood);
            AnimationDescriptor.Lookup.Delete(flames); AnimationDescriptor.Lookup.Delete(ashes);
        }
    }

    [Test]
    public void CampfireDurationsAndAnimationsAreBoundedAndTheNewPacketRoundTrips()
    {
        var result = new CampfireRecipeResult(); Assert.That(result.IsValid, Is.False);
        result.FireAnimationId = Guid.NewGuid(); result.AshesAnimationId = Guid.NewGuid(); Assert.That(result.IsValid, Is.True);
        result.FireSeconds = int.MaxValue; Assert.That(result.IsValid, Is.False);
        var packet = new CampfiresPacket { Fires = [new() { Id = Guid.NewGuid(), AnimationId = result.FireAnimationId, Burning = true, X = 4, Y = 7 }] };
        var copy = MessagePackSerializer.Deserialize<CampfiresPacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(copy.Fires.Single().AnimationId, Is.EqualTo(result.FireAnimationId));
        Assert.That(copy.Fires.Single().Burning, Is.True);
        foreach(var language in new[] { "en", "fr", "es" })
            foreach(var text in new[] { "Campfire", "Cook", "CAMPFIRE COOKING", "Configure campfire result", "Ashes animation" })
                Assert.That(QualityOfLifeText.Catalog.Entries[text].ContainsKey(language), Is.True);
    }
}
