using Intersect.Server.Database.PlayerData.Players;

namespace Intersect.Server.Networking;

internal static class CharacterSelectionGuildLookup
{
    internal static Dictionary<Guid, string> LoadNames(IQueryable<Guild> guilds, Guid[] guildIds) =>
        // Explicit Enumerable.Contains keeps this query translatable with newer
        // compilers that otherwise prefer the ReadOnlySpan overload for arrays.
        guildIds.Length == 0 ? [] : guilds.Where(guild => Enumerable.Contains(guildIds, guild.Id))
            .Select(guild => new { guild.Id, guild.Name }).ToDictionary(guild => guild.Id, guild => guild.Name);
}
