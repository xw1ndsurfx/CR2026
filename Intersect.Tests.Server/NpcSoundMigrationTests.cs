using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture]
public sealed class NpcSoundMigrationTests
{
    [Test]
    public void UpgradePreservesExistingNpcsAndStoresSoundChoices()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = new DbContext(new DbContextOptionsBuilder().UseSqlite(connection).Options);
        context.Database.ExecuteSqlRaw("CREATE TABLE Npcs (Id TEXT PRIMARY KEY); INSERT INTO Npcs (Id) VALUES ('existing');");
        var migration = new Intersect.Server.Migrations.Sqlite.Game.NpcSoundAmbience();
        foreach (var command in context.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations))
            context.Database.ExecuteSqlRaw(command.CommandText);
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT count(*) FROM Npcs WHERE Id='existing' AND DeathSound='' AND ProximitySound='' AND ProximitySoundRadius=5";
        Assert.That((long)query.ExecuteScalar()!, Is.EqualTo(1));
        context.Database.ExecuteSqlRaw("UPDATE Npcs SET DeathSound='death.wav', ProximitySound='growl.wav', ProximitySoundRadius=8 WHERE Id='existing'");
        query.CommandText = "SELECT count(*) FROM Npcs WHERE Id='existing' AND DeathSound='death.wav' AND ProximitySound='growl.wav' AND ProximitySoundRadius=8";
        Assert.That((long)query.ExecuteScalar()!, Is.EqualTo(1));
    }
}
