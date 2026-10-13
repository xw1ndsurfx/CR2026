using System.Linq.Expressions;
using System.Reflection;
using Intersect.Server.Database.PlayerData.Players;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Intersect.Tests.Server.Entities;

[TestFixture]
public sealed class ProfessionStoredVariableQueryTests
{
    private static Expression<Func<PlayerVariable, bool>> Filter(params Guid[] ids)
    {
        var runtime = typeof(PlayerVariable).Assembly.GetType("Intersect.Server.Professions.ProfessionRuntime", true)!;
        var method = runtime.GetMethod("StoredProfessionVariableFilter", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Expression<Func<PlayerVariable, bool>>)method.Invoke(null, new object[] { ids })!;
    }

    [Test]
    public void StartupFilterCanBeInterpretedAndMatchesOnlyConfiguredProfessions()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        // EF Core interprets captured query parameters; a span overload fails on this path.
        var predicate = Filter(first, second).Compile(preferInterpretation: true);
        Assert.Multiple(() =>
        {
            Assert.That(predicate(new PlayerVariable(first)), Is.True);
            Assert.That(predicate(new PlayerVariable(second)), Is.True);
            Assert.That(predicate(new PlayerVariable(Guid.NewGuid())), Is.False);
            Assert.That(Filter().Compile(preferInterpretation: true)(new PlayerVariable(first)), Is.False);
        });
    }

    [Test]
    public void StartupBatchTranslatesToSqliteWithoutOpeningADatabase()
    {
        using var context = new QueryContext();
        var query = context.Set<PlayerVariable>()
            .Where(Filter(Guid.NewGuid(), Guid.NewGuid()))
            .OrderBy(variable => variable.Id).Skip(500).Take(500);
        // ToQueryString exercises EF parameter extraction and translation, without execution or migrations.
        var sql = query.ToQueryString();
        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("WHERE"));
            Assert.That(sql, Does.Contain(" IN "));
            Assert.That(sql, Does.Contain("LIMIT"));
            Assert.That(sql, Does.Contain("OFFSET"));
            Assert.That(context.Database.GetDbConnection().State, Is.EqualTo(System.Data.ConnectionState.Closed));
        });
    }

    private sealed class QueryContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite("Data Source=:memory:");

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Ignore<Intersect.Server.Entities.Player>();
            model.Entity<PlayerVariable>().Ignore(variable => variable.Player);
        }
    }
}
