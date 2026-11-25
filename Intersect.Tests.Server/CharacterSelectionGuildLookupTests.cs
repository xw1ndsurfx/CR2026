using System.Reflection;
using Intersect.Server.Database.PlayerData.Players;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture]
public sealed class CharacterSelectionGuildLookupTests
{
    private sealed class GuildPreviewContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            var guild = builder.Entity<Guild>();
            foreach (var property in typeof(Guild).GetProperties().Where(p => p.Name != nameof(Guild.Id) && p.Name != nameof(Guild.Name)))
                guild.Ignore(property.Name);
            guild.HasKey(g => g.Id); guild.Property(g => g.Name); guild.ToTable("Guilds");
            // Conventions can discover bank/inventory entities before Ignore removes
            // their guild navigation. This preview fixture maps only the two columns
            // selected by the production query, with no game inventory schema.
            foreach (var type in builder.Model.GetEntityTypes().Select(entity => entity.ClrType)
                         .Where(type => type != typeof(Guild)).Distinct().ToArray())
                builder.Ignore(type);
        }
    }

    [Test]
    public void SelectionResolvesPersistedGuildIdsWithoutPlayerNavigation()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var context = new GuildPreviewContext(new DbContextOptionsBuilder().UseSqlite(connection).Options);
        context.Database.EnsureCreated();
        var id = Guid.NewGuid(); var unrelated = Guid.NewGuid(); var deleted = Guid.NewGuid();
        context.Database.ExecuteSqlInterpolated($"INSERT INTO Guilds (Id, Name) VALUES ({id}, {"TeamLogiklik"})");
        context.Database.ExecuteSqlInterpolated($"INSERT INTO Guilds (Id, Name) VALUES ({unrelated}, {"Other guild"})");
        var lookup = typeof(Intersect.Server.Networking.PacketSender).Assembly
            .GetType("Intersect.Server.Networking.CharacterSelectionGuildLookup", true)!
            .GetMethod("LoadNames", BindingFlags.Static | BindingFlags.NonPublic)!;
        var names = (Dictionary<Guid, string>)lookup.Invoke(null, [context.Set<Guild>(), new[] { id, id, deleted }])!;
        Assert.That(names, Has.Count.EqualTo(1));
        Assert.That(names[id], Is.EqualTo("TeamLogiklik"));
        Assert.That(names.GetValueOrDefault(deleted, ""), Is.Empty);
        Assert.That(names.ContainsKey(unrelated), Is.False);
        var empty = (Dictionary<Guid, string>)lookup.Invoke(null, [context.Set<Guild>(), Array.Empty<Guid>()])!;
        Assert.That(empty, Is.Empty);
        Assert.That(context.ChangeTracker.Entries(), Is.Empty, "A selection preview must not attach or mutate guild membership.");
    }
}
