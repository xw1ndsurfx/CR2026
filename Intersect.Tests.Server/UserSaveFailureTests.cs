using System.Runtime.CompilerServices;
using Intersect.Config;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Database.PlayerData.Security;
using Intersect.Server.Networking;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class UserSaveFailureTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void FailedSaveReturnsDatabaseFailureWithAnUnauthenticatedConnection(bool concurrencyFailure)
    {
        Options.EnsureCreated();
        var oldKill=Options.Instance.PlayerDatabase.KillServerOnConcurrencyException;
        Options.Instance.PlayerDatabase.KillServerOnConcurrencyException=false;
        var connectionId=Guid.NewGuid();
        var pendingClient=(Client)RuntimeHelpers.GetUninitializedObject(typeof(Client));
        GC.SuppressFinalize(pendingClient);
        Client.LookupByConnectionId.Add(connectionId,pendingClient);
        try
        {
            using var context=PlayerContext.Create(new DatabaseContextOptions
            {
                DatabaseType=DatabaseType.SQLite,
                ConnectionStringBuilder=new SqliteConnectionStringBuilder{DataSource=":memory:",Pooling=false},
                ReadOnly=false,AutoDetectChanges=false,DisableAutoInclude=true,
                QueryTrackingBehavior=QueryTrackingBehavior.TrackAll,LoggerFactory=NullLoggerFactory.Instance,
            });
            context.Database.OpenConnection();
            if(concurrencyFailure)context.Database.EnsureCreated();
            var user=new User{Name="save-failure-fixture",Email="fixture@example.invalid",Salt="fixture",Password="fixture",Power=UserRights.None};
            Assert.That(user.Save(context,force:true),Is.EqualTo(UserSaveResult.DatabaseFailure));
            Assert.That(pendingClient.User,Is.Null);
            Assert.That(Client.LookupByConnectionId.ContainsKey(connectionId),Is.True);
        }
        finally
        {
            Client.LookupByConnectionId.Remove(connectionId);
            Options.Instance.PlayerDatabase.KillServerOnConcurrencyException=oldKill;
        }
    }
}
