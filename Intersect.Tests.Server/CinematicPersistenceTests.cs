using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Entities.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture]
public sealed class CinematicPersistenceTests
{
    private static readonly Guid Gate = Guid.NewGuid(), Complete = Guid.NewGuid(), Choice = Guid.NewGuid();

    // Exercise the production PlayerVariable JSON column and ownership key on a real,
    // disposable SQLite file. This does not simulate a network login or User.Save.
    private sealed class VariableContext(string path) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Ignore<Player>();
            model.Entity<PlayerVariable>().Ignore(variable => variable.Player);
            model.Entity<PlayerVariable>().ToTable("Player_Variables");
            model.Entity<PlayerVariable>().HasIndex(variable => new { variable.VariableId, variable.PlayerId }).IsUnique();
        }
    }

    private static Player NewPlayer(Guid id)
    {
        Options.EnsureCreated();
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        GC.SuppressFinalize(player);
        player.Id = id; player.EntityLock = new(); player.EventLookup = new();
        player.Variables = new[] { Gate, Complete, Choice }.Select(id =>
            new PlayerVariable(id) { Value = new() { Value = false } }).ToList();
        return player;
    }

    private static void Set(Player player, Guid id, bool value)
    {
        var method = typeof(CommandProcessing).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == "ProcessVariableModification" && m.GetParameters()[1].ParameterType == typeof(BooleanVariableMod));
        method.Invoke(null, [new SetVariableCommand { VariableId = id, SyncParty = false },
            new BooleanVariableMod { Value = value }, player, null]);
    }

    private static bool Meets(Player player, Guid id) => Conditions.MeetsCondition(
        new VariableIsCondition { VariableId = id,
            Comparison = new BooleanVariableComparison { ComparingEqual = true, Value = true } }, player, null, null);

    private static void Save(string path, Player player)
    {
        using var db = new VariableContext(path);
        foreach (var variable in player.Variables)
        {
            var entry = variable.Id == Guid.Empty ? db.Add(variable) : db.Update(variable);
            entry.Property(v => v.PlayerId).CurrentValue = player.Id;
        }
        db.SaveChanges();
    }

    private static Player Reload(string path, Guid id)
    {
        var player = NewPlayer(id);
        using var db = new VariableContext(path);
        player.Variables = db.Set<PlayerVariable>().AsNoTracking().Where(v => v.PlayerId == id).ToList();
        Assert.That(player.Variables, Has.Count.EqualTo(3));
        return player;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ChoicesInteractionAndCompletionSurviveNewSqliteContextsWithoutUnlockingAnotherPlayer(bool branchB)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cinematic-variables-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = new VariableContext(path)) db.Database.EnsureCreated();
            var first = NewPlayer(Guid.NewGuid()); var second = NewPlayer(Guid.NewGuid());
            Save(path, first); Save(path, second);
            Set(first, Choice, branchB); Set(first, Gate, true); Set(first, Complete, true);
            Save(path, first);

            var returning = Reload(path, first.Id); var untouched = Reload(path, second.Id);
            Assert.Multiple(() =>
            {
                Assert.That(Meets(returning, Gate), Is.True);
                Assert.That(Meets(returning, Complete), Is.True);
                Assert.That(Meets(returning, Choice), Is.EqualTo(branchB));
                Assert.That(Meets(untouched, Gate), Is.False);
                Assert.That(Meets(untouched, Complete), Is.False);
                Assert.That(Meets(untouched, Choice), Is.False);
                Assert.That(returning.EventLookup, Is.Empty, "Persistent flags do not restore an in-flight cinematic.");
            });

            // A second player's completion and a replay/reset by the first must remain independent.
            Set(untouched, Gate, true); Set(untouched, Complete, true); Set(untouched, Choice, !branchB);
            Save(path, untouched);
            foreach (var id in new[] { Gate, Complete, Choice }) Set(returning, id, false);
            Save(path, returning);
            var reset = Reload(path, first.Id); var finished = Reload(path, second.Id);
            Assert.Multiple(() =>
            {
                Assert.That(Meets(reset, Gate), Is.False); Assert.That(Meets(reset, Complete), Is.False);
                Assert.That(Meets(finished, Gate), Is.True); Assert.That(Meets(finished, Complete), Is.True);
                Assert.That(Meets(finished, Choice), Is.EqualTo(!branchB));
            });
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
