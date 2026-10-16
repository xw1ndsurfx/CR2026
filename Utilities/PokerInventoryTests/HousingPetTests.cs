using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Framework.Core.Housing;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Server.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json;
internal static class HousingPetTests
{
    [ModuleInitializer] internal static void Run() { try { Test(); } catch (Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); } }
    private static void Check(bool value, string text) { if (!value) throw new Exception(text); }
    private static void Reject(Action action) { try { action(); throw new Exception("Unauthorized placement was accepted"); } catch (InvalidOperationException) { } }
    private static void Test()
    {
        var furniture = new FurnitureDefinition { Name = "Chair", Months = "2026-10, 2026-12", Tileset = "furniture.png", CurrencyItemId = Guid.NewGuid(), Price = 30 };
        Check(furniture.IsValid && furniture.Available(new DateTime(2026,10,31,23,59,59)) && !furniture.Available(new DateTime(2026,11,1)) && furniture.Available(new DateTime(2026,12,1)), "Catalog offers change at month boundaries");
        var house = new HouseProfile(); house.Furniture[furniture.Id] = new() { Definition = furniture, Quantity = 1 };
        var place = house.PlaceFurniture(furniture.Id, Guid.Empty, 3, 4, 0, 32, 26, 100);
        house.PlaceFurniture(furniture.Id, place.Id, 5, 6, 3, 32, 26, 100);
        Check(house.Placements.Count == 1 && place.X == 5 && place.Rotation == 3, "Moving furniture never consumes another stored copy");
        Reject(() => house.PlaceFurniture(furniture.Id, Guid.Empty, 1, 1, 0, 32, 26, 100));
        Reject(() => house.PlaceFurniture(Guid.NewGuid(), Guid.Empty, 1, 1, 0, 32, 26, 100));
        Reject(() => house.PlaceFurniture(furniture.Id, place.Id, 32, 1, 0, 32, 26, 100));
        Reject(() => house.PlaceFurniture(furniture.Id, place.Id, 1, 1, 4, 32, 26, 100));
        house.Public = true; house.Pets.Add(new HousePet { Id = Guid.NewGuid(), Name = "Fluffy", Sprite = "pet.png", X = 2, Y = 3 });
        var restored = HouseProfile.Read(JsonConvert.SerializeObject(house));
        Check(restored.Public && restored.Pets.Single().Name == "Fluffy" && restored.Placements.Single().Rotation == 3 && restored.Furniture[furniture.Id].Quantity == 1,
            "House privacy, purchased furniture, placements and housed pets survive saving and catalog expiration");
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var context = new DbContext(new DbContextOptionsBuilder().UseSqlite(connection).Options);
        using(var cmd=connection.CreateCommand()) { cmd.CommandText="CREATE TABLE Players (Id TEXT PRIMARY KEY, Name TEXT, Pets TEXT); INSERT INTO Players VALUES ('old', 'Existing', '{}');";cmd.ExecuteNonQuery(); }
        var migration = new Intersect.Server.Migrations.Sqlite.Player.PlayerHousing(); var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        migration.GetType().GetMethod("Up", BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(migration,[builder]);
        foreach(var sql in context.GetService<IMigrationsSqlGenerator>().Generate(builder.Operations)) { using var cmd=connection.CreateCommand();cmd.CommandText=sql.CommandText;cmd.ExecuteNonQuery(); }
        using(var cmd=connection.CreateCommand()) { cmd.CommandText="SELECT Name, Housing, Pets FROM Players WHERE Id='old'";using var reader=cmd.ExecuteReader();Check(reader.Read()&&reader.GetString(0)=="Existing"&&reader.IsDBNull(1)&&reader.GetString(2)=="{}","Housing migration preserves existing characters and pets"); }
        var previousOptions = Options.Instance;
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        var player = new Player(); GC.SuppressFinalize(player);
        var npc = (Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc)); GC.SuppressFinalize(npc);
        var descriptor = new NPCDescriptor(Guid.NewGuid()) { IsPet = true, PetSummonItemId = Guid.NewGuid() };
        NPCDescriptor.Lookup[descriptor.Id] = descriptor;
        var previousCooldown = Options.Instance.Npc.PetRecoverySeconds;
        try
        {
            Options.Instance.Npc.PetRecoverySeconds = 300;
            player.PetCollection.OwnedPets[descriptor.Id] = new PetProgress { Level = 7, Experience = 42 };
            player.PetCollection.ActivePetId = descriptor.Id;
            typeof(Player).GetProperty("ActivePet")!.SetValue(player,npc);
            var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); player.OnPetDied(npc);
            var saved=player.PetCollection.OwnedPets[descriptor.Id];
            Check(saved.RecoveryUntilUtc >= before+300000 && saved.RecoveryUntilUtc <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+300000,"Pet defeat starts the configured 5 minute penalty");
            var pets = player.PetsJson; player.PetsJson = pets;
            Check(player.PetCollection.OwnedPets[descriptor.Id].RecoveryUntilUtc == saved.RecoveryUntilUtc,"Pet penalty survives character persistence and relogging");
            player.TryActivatePetItem(descriptor.PetSummonItemId);
            Check(player.PetCollection.ActivePetId == Guid.Empty && player.ActivePet == null,"Summon items cannot bypass the death penalty");
            typeof(Player).GetMethod("SummonPet",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(player,[descriptor.Id]);
            Check(player.PetCollection.ActivePetId==Guid.Empty,"Pet summon command cannot bypass the death penalty");
            player.House.Pets.Add(new HousePet { Id=descriptor.Id }); player.HousePet(descriptor.Id,true);
            Check(player.PetCollection.OwnedPets[descriptor.Id].Housed && player.PetCollection.OwnedPets[descriptor.Id].Level==7,"Storing a pet preserves progression and removes the active companion");
        }
        finally { NPCDescriptor.Lookup.DeleteAt(descriptor.Id); Options.Instance.Npc.PetRecoverySeconds=previousCooldown; typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, previousOptions); }
        var runtime=typeof(Player).Assembly.GetType("Intersect.Server.Housing.HousingRuntime",true)!;
        try { runtime.GetMethod("RequireInside",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[player]); throw new Exception("Outside decoration accepted"); }
        catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException) { }
        var back = new HouseReturnLocation { MapId = Guid.NewGuid(), InstanceId = Guid.Empty, X = 9, Y = 12 };
        player.House.ReturnLocation = back;
        runtime.GetMethod("RestoreLoginLocation",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[player]);
        Check(player.MapId == back.MapId && player.MapInstanceId == back.InstanceId && player.X == 9 && player.Y == 12 && player.House.ReturnLocation == null,
            "Reconnecting restores the saved destination before shared-instance login can change party membership");
        var invasion = new Intersect.Framework.Core.WorldEvents.Invasions.InvasionDefinition
            { DefeatMessage = "{name} fell near {island}: {target}", DefeatSoundFirst = "loss.wav", DefeatSoundSecond = "farewell.wav" };
        var savedInvasion = System.Text.Json.JsonSerializer.Deserialize<Intersect.Framework.Core.WorldEvents.Invasions.InvasionDefinition>(System.Text.Json.JsonSerializer.Serialize(invasion))!;
        Check(savedInvasion.DefeatMessage == invasion.DefeatMessage && savedInvasion.DefeatSoundFirst == "loss.wav" && savedInvasion.DefeatSoundSecond == "farewell.wav", "Invasion defeat message and ordered sound choices survive configuration persistence");
        System.Console.WriteLine("PASS housing catalog boundaries, furniture ownership/stock/coordinates, persistence, real migration and pet recovery enforcement");
    }
}
