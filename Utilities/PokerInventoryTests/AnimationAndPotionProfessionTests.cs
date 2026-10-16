using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Framework.Core.Professions;
using Intersect.Server.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
internal static class AnimationAndPotionProfessionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try { RunCore(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }
    private static void RunCore()
    {
        var alchemist = new ProfessionDefinition { Name = "Alchemist", BaseExperience = 1000 };
        var other = new ProfessionDefinition { Name = "Custom brewing", PotionExperience = true, BaseExperience = 1000 };
        var config = new ProfessionConfiguration { Professions = [alchemist] };
        Check(config.FindPotionProfession() == alchemist, "Existing Alchemist profession is detected without configuration changes");
        config.Professions = [alchemist, other]; Check(config.FindPotionProfession() == other, "Explicit potion profession takes priority over a name match");
        var roundtrip = ProfessionConfiguration.FromJson(config.ToJson()); Check(roundtrip.FindPotionProfession()?.Id == other.Id, "Potion XP link survives configuration persistence");
        alchemist.PotionExperience = true; Check(!config.IsStructurallyValid, "Multiple potion XP targets are rejected"); alchemist.PotionExperience = false;
        var assembly = typeof(Player).Assembly;
        var runtime = assembly.GetType("Intersect.Server.Professions.ProfessionRuntime", true)!;
        var configuration = assembly.GetType("Intersect.Server.Professions.ProfessionConfigurationRuntime", true)!;
        var cache = configuration.GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = cache.GetValue(null); var options = Options.Instance;
        try
        {
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options()); cache.SetValue(null, config);
            var player = new Player(); GC.SuppressFinalize(player);
            runtime.GetMethod("AwardPotionCompletion", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [player, 25L]);
            var xp = (long)runtime.GetMethod("GetExperience", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [player, other.Id])!;
            Check(xp == 25 && player.GetVariable(alchemist.Id) == null, "Real profession runtime awards potion XP to the linked profession only");
        }
        finally { cache.SetValue(null, previous); typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, options); }
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var context = new DbContext(new DbContextOptionsBuilder().UseSqlite(connection).Options);
        using (var command = connection.CreateCommand()) { command.CommandText = "CREATE TABLE Animations (Id TEXT PRIMARY KEY); INSERT INTO Animations VALUES ('existing');"; command.ExecuteNonQuery(); }
        var migration = new Intersect.Server.Migrations.Sqlite.Game.AnimationLayerVisuals();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        Check(builder.Operations.Count == 6, "Both animation layers receive offset and transparency columns");
        foreach (var generated in context.GetService<IMigrationsSqlGenerator>().Generate(builder.Operations))
        { using var command = connection.CreateCommand(); command.CommandText = generated.CommandText; command.ExecuteNonQuery(); }
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Lower_OffsetX, Lower_OffsetY, Lower_TransparencyPercent, Upper_OffsetX, Upper_OffsetY, Upper_TransparencyPercent FROM Animations WHERE Id='existing'";
            using var reader = command.ExecuteReader(); Check(reader.Read() && Enumerable.Range(0, 6).All(i => reader.GetInt32(i) == 0), "Real SQLite migration preserves old animations with zero offsets and full opacity");
        }
        using (var command = connection.CreateCommand()) { command.CommandText = "CREATE TABLE Spells (Id TEXT PRIMARY KEY, Name TEXT); INSERT INTO Spells VALUES ('existing', 'Old spell');"; command.ExecuteNonQuery(); }
        var rarityMigration = new Intersect.Server.Migrations.Sqlite.Game.SpellRarity();
        var rarityBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        rarityMigration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(rarityMigration, [rarityBuilder]);
        foreach (var generated in context.GetService<IMigrationsSqlGenerator>().Generate(rarityBuilder.Operations))
        { using var command = connection.CreateCommand(); command.CommandText = generated.CommandText; command.ExecuteNonQuery(); }
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Rarity, Name FROM Spells WHERE Id='existing'";
            using var reader = command.ExecuteReader(); Check(reader.Read() && reader.GetInt32(0) == 0 && reader.GetString(1) == "Old spell", "Spell rarity migration preserves existing spells with the default tier");
        }
        var spell = new Intersect.GameObjects.SpellDescriptor(Guid.NewGuid()) { Rarity = 4 };
        var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<Intersect.GameObjects.SpellDescriptor>(Newtonsoft.Json.JsonConvert.SerializeObject(spell))!;
        Check(restored.Rarity == 4, "Spell rarity survives descriptor JSON used for saves and client delivery");
        System.Console.WriteLine("PASS potion profession XP integration and real animation visuals schema migration");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
