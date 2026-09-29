using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using System.Text.RegularExpressions;
using System.Net;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.PlayerClass;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.GameObjects;
using Intersect.Framework.Core.MiniGames.Configuration;
using Intersect.Framework.Core.MiniGames.Cooking;
using Intersect.Framework.Core.MiniGames.Potions;
using Intersect.Framework.Core.WorldEvents.Invasions;
using Intersect.Models;
using Intersect.Server.WorldEvents.Invasions;
using Intersect.Server.MiniGames;
using Intersect.Server.Database;
using Intersect.Server.Entities;
using Intersect.Server.Leaderboards;
using Intersect.Server.Professions;
using Intersect.Server.Web.Http;
using Intersect.Server.Web.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Intersect.Server.Web.Controllers.Api.V1;

[Route("api/v1/wiki")]
[AllowAnonymous]
public sealed class WikiController : IntersectController
{
    private static readonly HashSet<GameObjectType> PublicTypes =
    [
        GameObjectType.Class,
        GameObjectType.Item,
        GameObjectType.Npc,
        GameObjectType.Quest,
        GameObjectType.Resource,
        GameObjectType.Shop,
        GameObjectType.Spell,
        GameObjectType.CraftTables,
        GameObjectType.Crafts,
        GameObjectType.Map,
        GameObjectType.Event,
    ];

    public sealed record WikiGameObjectSummary(
        Guid Id,
        string Name,
        string Type,
        string? Subtitle = null,
        string? Icon = null,
        string? ImageUrl = null
    );

    public sealed record WikiCatalogEntry(string Slug, string Type, string Label);

    public sealed record WikiPublicDetail(
        Guid Id,
        string Name,
        string Type,
        string? ImageUrl,
        IReadOnlyDictionary<string, object?> Facts
    );

    public sealed record WikiMapLayoutEntry(
        Guid Id,
        string Name,
        int X,
        int Y,
        bool IsIndoors,
        string Zone,
        int Revision,
        string PreviewUrl
    );

    public sealed record WikiInvasionScheduleEntry(
        string Name,
        string TargetMap,
        IReadOnlyList<string> Days,
        string StartTime,
        DateTimeOffset? NextStartAt,
        int WaveCount,
        bool HasBossWave,
        long BaseExperience,
        bool HasPreStartCinematic
    );

    public sealed record WikiInvasionScheduleResponse(
        DateTimeOffset GeneratedAt,
        IReadOnlyList<WikiInvasionScheduleEntry> Upcoming
    );


    public sealed record WikiPotionRequirement(
        string Family,
        int Level,
        int Needed
    );

    public sealed record WikiPotionRecipe(
        string Name,
        int RequiredLevel,
        string OutputItem,
        int OutputQuantity,
        string? OutputImageUrl,
        int CompletionExperience,
        bool RequiresEventUnlock,
        IReadOnlyList<WikiPotionRequirement> Requirements
    );

    public sealed record WikiCookingIngredient(
        string Item,
        int Quantity,
        string? ImageUrl
    );

    public sealed record WikiCookingStage(
        string Type,
        int Difficulty,
        int DurationSeconds,
        int RequiredActions,
        string Assignment
    );

    public sealed record WikiCookingOutput(
        string Quality,
        string Item,
        int Quantity,
        string? ImageUrl,
        long SoloProfessionExperience
    );

    public sealed record WikiCookingRecipe(
        string Name,
        string Profession,
        int RequiredProfessionLevel,
        long BaseProfessionExperience,
        bool AllowSolo,
        bool AllowCoop,
        bool RequireCoop,
        bool RequiresEventUnlock,
        IReadOnlyList<WikiCookingIngredient> Ingredients,
        IReadOnlyList<WikiCookingStage> Stages,
        IReadOnlyList<WikiCookingOutput> Outputs
    );


    public sealed record WikiLevelLeaderboardEntry(
        int Rank,
        string Name,
        int Level,
        long Experience,
        string Class,
        string? Guild,
        string? Title,
        int ExperienceBonusPercent,
        int ConsecutiveDays
    );

    public sealed record WikiLevelLeaderboardResponse(
        DateTimeOffset GeneratedAt,
        IReadOnlyList<WikiLevelLeaderboardEntry> Players
    );


    public sealed record WikiQuestTask(
        int Number,
        string Objective,
        string? Target,
        int Quantity,
        string Description,
        bool NavigationEnabled,
        string? GuideResource
    );

    public sealed record WikiQuestDetail(
        string Name,
        string StartDescription,
        string InProgressDescription,
        string EndDescription,
        string BeforeDescription,
        string Category,
        bool Repeatable,
        bool Quitable,
        IReadOnlyList<string> Locations,
        IReadOnlyList<WikiQuestTask> Tasks,
        IReadOnlyList<string> Rewards
    );


    public sealed record WikiMiniGameLeaderboardEntry(
        int Rank,
        string Name,
        int Level,
        long Experience,
        long Wins
    );

    public sealed record WikiMiniGameLeaderboard(
        string Key,
        string Name,
        IReadOnlyList<WikiMiniGameLeaderboardEntry> Players
    );

    public sealed record WikiMiniGameLeaderboardResponse(
        DateTimeOffset GeneratedAt,
        IReadOnlyList<WikiMiniGameLeaderboard> Leaderboards
    );

    public sealed record WikiProfessionLeaderboardEntry(
        int Rank,
        string Name,
        int Level,
        long Experience
    );

    public sealed record WikiProfessionLeaderboard(
        string Key,
        string Name,
        int MaximumLevel,
        IReadOnlyList<WikiProfessionLeaderboardEntry> Players
    );

    public sealed record WikiProfessionLeaderboardResponse(
        DateTimeOffset GeneratedAt,
        IReadOnlyList<WikiProfessionLeaderboard> Leaderboards
    );


    public sealed record WikiGuildSummary(
        Guid Id,
        string Name,
        DateTime FoundingDate,
        int MemberCount,
        string? Leader,
        int HighestMemberLevel,
        double AverageMemberLevel
    );

    public sealed record WikiGuildMember(
        string Name,
        string Rank,
        int Level,
        string Class,
        DateTime JoinedAt
    );

    public sealed record WikiGuildDetail(
        Guid Id,
        string Name,
        DateTime FoundingDate,
        int MemberCount,
        string? Leader,
        int HighestMemberLevel,
        double AverageMemberLevel,
        IReadOnlyList<WikiGuildMember> Members
    );

    [HttpGet("leaderboard/minigames")]
    [ProducesResponseType(typeof(WikiMiniGameLeaderboardResponse), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult MiniGameLeaderboards([FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 100);

        var leaderboards = LeaderboardDataRuntime.MiniGames(limit)
            .GroupBy(row => new { row.GameKey, row.GameName })
            .OrderBy(group => group.Key.GameName)
            .Select(group => new WikiMiniGameLeaderboard(
                group.Key.GameKey,
                group.Key.GameName,
                group.Select((row, index) => new WikiMiniGameLeaderboardEntry(
                    index + 1,
                    row.PlayerName,
                    row.Level,
                    row.Experience,
                    row.Wins
                )).ToArray()
            ))
            .ToArray();

        return Ok(new WikiMiniGameLeaderboardResponse(DateTimeOffset.UtcNow, leaderboards));
    }

    [HttpGet("leaderboard/professions")]
    [ProducesResponseType(typeof(WikiProfessionLeaderboardResponse), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult ProfessionLeaderboards([FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 100);

        var leaderboards = LeaderboardDataRuntime.Professions(limit)
            .GroupBy(row => new { row.ProfessionKey, row.ProfessionName, row.MaximumLevel })
            .OrderBy(group => group.Key.ProfessionName)
            .Select(group => new WikiProfessionLeaderboard(
                group.Key.ProfessionKey,
                group.Key.ProfessionName,
                group.Key.MaximumLevel,
                group.Select((row, index) => new WikiProfessionLeaderboardEntry(
                    index + 1,
                    row.PlayerName,
                    row.Level,
                    row.Experience
                )).ToArray()
            ))
            .ToArray();

        return Ok(new WikiProfessionLeaderboardResponse(DateTimeOffset.UtcNow, leaderboards));
    }

    [HttpGet("leaderboard/levels")]
    [ProducesResponseType(typeof(WikiLevelLeaderboardResponse), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult LevelLeaderboard([FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 100);

        using var context = DbInterface.CreatePlayerContext();
        var candidates = context.Players
            .AsNoTracking()
            .OrderByDescending(player => player.Level)
            .ThenByDescending(player => player.Exp)
            .ThenBy(player => player.Name)
            .Take(Math.Max(limit, 100))
            .Select(player => new
            {
                player.Id,
                player.Name,
                player.Level,
                player.Exp,
                player.ClassId,
                GuildName = player.Guild != null ? player.Guild.Name : null,
            })
            .ToDictionary(player => player.Id);

        foreach (var online in Player.OnlinePlayersSnapshot())
        {
            candidates[online.Id] = new
            {
                online.Id,
                online.Name,
                online.Level,
                online.Exp,
                online.ClassId,
                GuildName = online.Guild?.Name,
            };
        }

        var rows = candidates.Values
            .OrderByDescending(player => player.Level)
            .ThenByDescending(player => player.Exp)
            .ThenBy(player => player.Name)
            .Take(limit)
            .ToArray();

        var titleStatuses = LeaderboardTitleRuntime.Snapshot();

        var players = rows
            .Select((player, index) =>
            {
                titleStatuses.TryGetValue(player.Id, out var title);

                return new WikiLevelLeaderboardEntry(
                    index + 1,
                    player.Name,
                    player.Level,
                    player.Exp,
                    ClassDescriptor.GetName(player.ClassId),
                    player.GuildName,
                    title?.Title,
                    title?.ExperienceBonusPercent ?? 0,
                    title?.ConsecutiveDays ?? 0
                );
            })
            .ToArray();

        return Ok(new WikiLevelLeaderboardResponse(DateTimeOffset.UtcNow, players));
    }

    [HttpGet("guilds")]
    [ProducesResponseType(typeof(IReadOnlyList<WikiGuildSummary>), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult Guilds([FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 250);

        using var context = DbInterface.CreatePlayerContext();
        var guilds = context.Guilds
            .AsNoTracking()
            .OrderBy(guild => guild.Name)
            .Take(limit)
            .Select(guild => new
            {
                guild.Id,
                guild.Name,
                guild.FoundingDate,
            })
            .ToArray();

        var guildIds = guilds.Select(guild => guild.Id).ToArray();
        var members = context.Players
            .AsNoTracking()
            .Where(player => player.GuildId.HasValue && guildIds.Contains(player.GuildId.Value))
            .Select(player => new
            {
                GuildId = player.GuildId!.Value,
                player.Name,
                player.GuildRank,
                player.Level,
            })
            .ToArray()
            .GroupBy(player => player.GuildId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var result = guilds
            .Select(guild =>
            {
                members.TryGetValue(guild.Id, out var guildMembers);
                guildMembers ??= [];

                var leader = guildMembers
                    .Where(member => member.GuildRank == 0)
                    .OrderBy(member => member.Name)
                    .Select(member => member.Name)
                    .FirstOrDefault();

                var highestLevel = guildMembers.Length == 0
                    ? 0
                    : guildMembers.Max(member => member.Level);

                var averageLevel = guildMembers.Length == 0
                    ? 0
                    : Math.Round(guildMembers.Average(member => member.Level), 1);

                return new WikiGuildSummary(
                    guild.Id,
                    guild.Name,
                    guild.FoundingDate,
                    guildMembers.Length,
                    leader,
                    highestLevel,
                    averageLevel
                );
            })
            .ToArray();

        return Ok(result);
    }

    [HttpGet("guilds/{guildId:guid}")]
    [ProducesResponseType(typeof(WikiGuildDetail), (int)HttpStatusCode.OK, ContentTypes.Json)]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.NotFound, ContentTypes.Json)]
    public IActionResult GuildDetail(Guid guildId)
    {
        if (guildId == Guid.Empty)
        {
            return NotFound("No published guild was found.");
        }

        using var context = DbInterface.CreatePlayerContext();
        var guild = context.Guilds
            .AsNoTracking()
            .Where(value => value.Id == guildId)
            .Select(value => new
            {
                value.Id,
                value.Name,
                value.FoundingDate,
            })
            .FirstOrDefault();

        if (guild == null)
        {
            return NotFound("No published guild was found.");
        }

        var rows = context.Players
            .AsNoTracking()
            .Where(player => player.GuildId == guildId)
            .Select(player => new
            {
                player.Name,
                player.GuildRank,
                player.Level,
                player.ClassId,
                player.GuildJoinDate,
            })
            .ToArray()
            .OrderBy(player => player.GuildRank)
            .ThenByDescending(player => player.Level)
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rankDefinitions = Options.Instance.Guild.Ranks ?? [];
        string RankName(int rank) =>
            rank >= 0 && rank < rankDefinitions.Length && !string.IsNullOrWhiteSpace(rankDefinitions[rank].Title)
                ? rankDefinitions[rank].Title
                : $"Rank {rank + 1}";

        var members = rows
            .Select(player => new WikiGuildMember(
                player.Name,
                RankName(player.GuildRank),
                player.Level,
                ClassDescriptor.GetName(player.ClassId),
                player.GuildJoinDate
            ))
            .ToArray();

        var leader = rows
            .Where(player => player.GuildRank == 0)
            .Select(player => player.Name)
            .FirstOrDefault();

        var highestLevel = rows.Length == 0 ? 0 : rows.Max(player => player.Level);
        var averageLevel = rows.Length == 0 ? 0 : Math.Round(rows.Average(player => player.Level), 1);

        return Ok(new WikiGuildDetail(
            guild.Id,
            guild.Name,
            guild.FoundingDate,
            members.Length,
            leader,
            highestLevel,
            averageLevel,
            members
        ));
    }

    [HttpGet("catalog")]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult Catalog()
    {
        var gameObjects = new[]
        {
            new WikiCatalogEntry("classes", nameof(GameObjectType.Class), "Classes"),
            new WikiCatalogEntry("items", nameof(GameObjectType.Item), "Items"),
            new WikiCatalogEntry("npcs", nameof(GameObjectType.Npc), "NPC"),
            new WikiCatalogEntry("quests", nameof(GameObjectType.Quest), "Quêtes"),
            new WikiCatalogEntry("resources", nameof(GameObjectType.Resource), "Ressources"),
            new WikiCatalogEntry("shops", nameof(GameObjectType.Shop), "Boutiques"),
            new WikiCatalogEntry("spells", nameof(GameObjectType.Spell), "Sorts"),
            new WikiCatalogEntry("crafting-tables", nameof(GameObjectType.CraftTables), "Tables de crafting"),
            new WikiCatalogEntry("crafts", nameof(GameObjectType.Crafts), "Recettes"),
            new WikiCatalogEntry("maps", nameof(GameObjectType.Map), "Cartes"),
            new WikiCatalogEntry("events", nameof(GameObjectType.Event), "Événements"),
        };

        return Ok(new { gameObjects, miniGames = MiniGameCatalog.All });
    }

    [HttpGet("gameobjects/{gameObjectType}")]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.BadRequest, ContentTypes.Json)]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.NotFound, ContentTypes.Json)]
    [ProducesResponseType(typeof(DataPage<WikiGameObjectSummary>), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult List(GameObjectType gameObjectType, [FromQuery] PagingInfo pageInfo)
    {
        if (!PublicTypes.Contains(gameObjectType))
        {
            return NotFound($"Game object type '{gameObjectType}' is not published by the wiki API.");
        }

        if (!gameObjectType.TryGetLookup(out var lookup))
        {
            return BadRequest($"Invalid {nameof(GameObjectType)} '{gameObjectType}'.");
        }

        pageInfo.Page = Math.Max(pageInfo.Page, 0);
        pageInfo.PageSize = Math.Max(Math.Min(pageInfo.PageSize, 100), 5);

        IEnumerable<IDatabaseObject> values = lookup.Values;
        if (gameObjectType == GameObjectType.Event)
        {
            values = values.OfType<EventDescriptor>().Where(descriptor => descriptor.CommonEvent);
        }

        var ordered = values.OrderBy(value => value.Name).ToArray();
        var questLocationIndex = gameObjectType == GameObjectType.Quest
            ? BuildQuestLocationIndex()
            : null;
        var entries = ordered
            .Skip(pageInfo.Page * pageInfo.PageSize)
            .Take(pageInfo.PageSize)
            .Select(value => ToPublicSummary(value, questLocationIndex))
            .ToArray();

        return Ok(new DataPage<WikiGameObjectSummary>(
            Total: ordered.Length,
            Page: pageInfo.Page,
            PageSize: pageInfo.PageSize,
            Count: entries.Length,
            Values: entries
        ));
    }

    [HttpGet("gameobjects/{gameObjectType}/{objectId:guid}")]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.BadRequest, ContentTypes.Json)]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.NotFound, ContentTypes.Json)]
    [ProducesResponseType(typeof(WikiPublicDetail), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult Detail(GameObjectType gameObjectType, Guid objectId)
    {
        if (!PublicTypes.Contains(gameObjectType))
        {
            return NotFound($"Game object type '{gameObjectType}' is not published by the wiki API.");
        }

        if (objectId == default)
        {
            return BadRequest("Invalid id.");
        }

        if (!gameObjectType.TryGetLookup(out var lookup))
        {
            return BadRequest($"Invalid {nameof(GameObjectType)} '{gameObjectType}'.");
        }

        if (!lookup.TryGetValue(objectId, out var gameObject))
        {
            return NotFound($"No published {gameObjectType} was found.");
        }

        if (gameObjectType == GameObjectType.Event &&
            gameObject is EventDescriptor eventDescriptor &&
            !eventDescriptor.CommonEvent)
        {
            return NotFound("No published event was found.");
        }

        return Ok(ToPublicDetail(gameObject));
    }

    [HttpGet("quests/{questId:guid}")]
    [ProducesResponseType(typeof(WikiQuestDetail), (int)HttpStatusCode.OK, ContentTypes.Json)]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.NotFound, ContentTypes.Json)]
    public IActionResult QuestDetail(Guid questId)
    {
        if (questId == Guid.Empty || QuestDescriptor.Get(questId) is not { } quest)
        {
            return NotFound("No published quest was found.");
        }

        var locationIndex = BuildQuestLocationIndex();
        locationIndex.TryGetValue(quest.Id, out var locations);

        var tasks = quest.Tasks
            .Select((task, index) => new WikiQuestTask(
                index + 1,
                task.Objective.ToString(),
                QuestTaskTarget(task),
                Math.Max(0, task.Quantity),
                task.Description ?? string.Empty,
                task.ShowNavigationArrow,
                task.GuideResourceId != Guid.Empty
                    ? ResourceDescriptor.GetName(task.GuideResourceId)
                    : null
            ))
            .ToArray();

        var category = !string.IsNullOrWhiteSpace(quest.InProgressCategory)
            ? quest.InProgressCategory
            : quest.UnstartedCategory;

        return Ok(new WikiQuestDetail(
            quest.Name,
            quest.StartDescription ?? string.Empty,
            quest.InProgressDescription ?? string.Empty,
            quest.EndDescription ?? string.Empty,
            quest.BeforeDescription ?? string.Empty,
            category ?? string.Empty,
            quest.Repeatable,
            quest.Quitable,
            locations ?? [],
            tasks,
            ExtractQuestRewards(quest)
        ));
    }

    [HttpGet("maps/layout")]
    [ProducesResponseType(typeof(IReadOnlyList<WikiMapLayoutEntry>), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult MapLayout()
    {
        var maps = MapDescriptor.Lookup.Values.OfType<MapDescriptor>().OrderBy(map => map.Name).ToArray();
        var byId = maps.ToDictionary(map => map.Id);
        var positions = new Dictionary<Guid, (int X, int Y)>();
        var occupied = new HashSet<(int X, int Y)>();
        var result = new List<WikiMapLayoutEntry>();
        var componentOffset = 0;

        foreach (var root in maps)
        {
            if (positions.ContainsKey(root.Id))
            {
                continue;
            }

            while (occupied.Contains((componentOffset, 0)))
            {
                componentOffset += 2;
            }

            positions[root.Id] = (componentOffset, 0);
            occupied.Add((componentOffset, 0));
            var queue = new Queue<MapDescriptor>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var (x, y) = positions[current.Id];

                TryPlace(current.Up, x, y - 1);
                TryPlace(current.Down, x, y + 1);
                TryPlace(current.Left, x - 1, y);
                TryPlace(current.Right, x + 1, y);

                void TryPlace(Guid neighborId, int nx, int ny)
                {
                    if (neighborId == Guid.Empty || positions.ContainsKey(neighborId) || !byId.TryGetValue(neighborId, out var neighbor))
                    {
                        return;
                    }

                    while (occupied.Contains((nx, ny)))
                    {
                        nx++;
                    }

                    positions[neighborId] = (nx, ny);
                    occupied.Add((nx, ny));
                    queue.Enqueue(neighbor);
                }
            }

            componentOffset = occupied.Count == 0 ? 0 : occupied.Max(p => p.X) + 3;
        }

        foreach (var map in maps)
        {
            var (x, y) = positions[map.Id];
            result.Add(new WikiMapLayoutEntry(
                map.Id,
                map.Name,
                x,
                y,
                map.IsIndoors,
                map.ZoneType.ToString(),
                map.Revision,
                $"/api/v1/wiki/maps/{map.Id}/preview?v={map.Revision}"
            ));
        }

        return Ok(result);
    }

    [HttpGet("maps/{mapId:guid}/preview")]
    [Produces("image/png")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.NotFound, ContentTypes.Json)]
    public IActionResult MapPreview(Guid mapId)
    {
        if (mapId == Guid.Empty || !MapDescriptor.Lookup.TryGetValue(mapId, out var mapObject) || mapObject is not MapDescriptor map)
        {
            return NotFound("No published map preview was found.");
        }

        var previewDirectory = Path.Combine(AppContext.BaseDirectory, "resources", "wiki", "maps");
        var revisionPath = Path.Combine(previewDirectory, $"{map.Id:N}-{map.Revision}.png");
        var stablePath = Path.Combine(previewDirectory, $"{map.Id:N}.png");
        var previewPath = System.IO.File.Exists(revisionPath)
            ? revisionPath
            : System.IO.File.Exists(stablePath)
                ? stablePath
                : null;

        if (previewPath == null)
        {
            return NotFound("Map preview is not available yet.");
        }

        Response.Headers.CacheControl = "public,max-age=3600,immutable";
        Response.Headers.ETag = $"\"map-{map.Id:N}-{map.Revision}\"";
        return PhysicalFile(previewPath, "image/png");
    }


    [HttpGet("invasions")]
    [ProducesResponseType(typeof(WikiInvasionScheduleResponse), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult Invasions()
    {
        var now = DateTimeOffset.Now;
        var upcoming = InvasionConfigurationRuntime.Current.Invasions
            .Where(invasion => invasion.Enabled)
            .Select(invasion =>
            {
                var targetMap = MapDescriptor.Lookup.TryGetValue(invasion.TargetMapId, out var mapObject) &&
                                mapObject is MapDescriptor map
                    ? map.Name
                    : "Unknown";

                return new WikiInvasionScheduleEntry(
                    invasion.Name,
                    targetMap,
                    ScheduleDayNames(invasion),
                    $"{invasion.StartHour:00}:{invasion.StartMinute:00}",
                    NextOccurrence(invasion, now),
                    invasion.Waves?.Length ?? 0,
                    invasion.Waves?.Any(wave => wave.Spawns?.Any(spawn => spawn.IsBoss) == true) == true,
                    invasion.RewardExperience,
                    invasion.PreStartCinematicEnabled
                );
            })
            .OrderBy(entry => entry.NextStartAt ?? DateTimeOffset.MaxValue)
            .ThenBy(entry => entry.Name)
            .ToArray();

        return Ok(new WikiInvasionScheduleResponse(now, upcoming));
    }

    private static DateTimeOffset? NextOccurrence(InvasionDefinition invasion, DateTimeOffset now)
    {
        for (var dayOffset = 0; dayOffset <= 7; ++dayOffset)
        {
            var date = now.Date.AddDays(dayOffset);
            var candidate = new DateTimeOffset(
                date.Year,
                date.Month,
                date.Day,
                invasion.StartHour,
                invasion.StartMinute,
                0,
                now.Offset
            );

            if (!invasion.RunsOn(candidate.DayOfWeek) || candidate < now)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static IReadOnlyList<string> ScheduleDayNames(InvasionDefinition invasion)
    {
        var days = new List<string>(7);
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            if (invasion.RunsOn(day))
            {
                days.Add(day.ToString());
            }
        }

        return days;
    }


    [HttpGet("minigames/potions/recipes")]
    [ProducesResponseType(typeof(IReadOnlyList<WikiPotionRecipe>), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult PotionRecipes()
    {
        var recipes = (RewardConfigurationRuntime.Current.PotionRecipes ?? [])
            .Where(recipe => recipe.IsStructurallyValid)
            .OrderBy(recipe => recipe.RequiredLevel)
            .ThenBy(recipe => recipe.Name)
            .Select(recipe =>
            {
                var output = ItemDescriptor.Get(recipe.OutputItemId);
                return new WikiPotionRecipe(
                    recipe.Name,
                    recipe.RequiredLevel,
                    output?.Name ?? "Unknown item",
                    recipe.OutputQuantity,
                    output?.ImageUrl,
                    recipe.CompletionExperience,
                    recipe.UnlockPlayerVariableId != Guid.Empty,
                    (recipe.Requirements ?? [])
                        .Select(requirement => new WikiPotionRequirement(
                            requirement.Family.ToString(),
                            requirement.Level,
                            requirement.Needed
                        ))
                        .ToArray()
                );
            })
            .ToArray();

        return Ok(recipes);
    }

    [HttpGet("minigames/cooking/recipes")]
    [ProducesResponseType(typeof(IReadOnlyList<WikiCookingRecipe>), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult CookingRecipes()
    {
        var professions = ProfessionConfigurationRuntime.Current;

        var recipes = (RewardConfigurationRuntime.Current.CookingRecipes ?? [])
            .Where(recipe => recipe.IsStructurallyValid)
            .OrderBy(recipe => recipe.RequiredProfessionLevel)
            .ThenBy(recipe => recipe.Name)
            .Select(recipe =>
            {
                var profession = professions.Find(recipe.ProfessionId);
                var ingredients = (recipe.Ingredients ?? [])
                    .Select(ingredient =>
                    {
                        var item = ItemDescriptor.Get(ingredient.ItemId);
                        return new WikiCookingIngredient(
                            item?.Name ?? "Unknown item",
                            ingredient.Quantity,
                            item?.ImageUrl
                        );
                    })
                    .ToArray();

                var stages = (recipe.Stages ?? [])
                    .Select(stage => new WikiCookingStage(
                        stage.Type.ToString(),
                        stage.Difficulty,
                        stage.DurationSeconds,
                        stage.RequiredActions,
                        stage.Assignment.ToString()
                    ))
                    .ToArray();

                var outputs = (recipe.Outputs ?? [])
                    .OrderBy(output => output.Quality)
                    .Select(output =>
                    {
                        var item = ItemDescriptor.Get(output.ItemId);
                        var soloExperience = Math.Max(
                            1L,
                            (long)Math.Round(
                                recipe.ProfessionExperience *
                                CookingRecipeDefinition.ExperienceMultiplier(output.Quality),
                                MidpointRounding.AwayFromZero
                            )
                        );

                        return new WikiCookingOutput(
                            output.Quality.ToString(),
                            item?.Name ?? "Unknown item",
                            output.Quantity,
                            item?.ImageUrl,
                            soloExperience
                        );
                    })
                    .ToArray();

                return new WikiCookingRecipe(
                    recipe.Name,
                    profession?.Name ?? "Cooking",
                    recipe.RequiredProfessionLevel,
                    recipe.ProfessionExperience,
                    recipe.AllowSolo,
                    recipe.AllowCoop,
                    recipe.RequireCoop,
                    recipe.UnlockPlayerVariableId != Guid.Empty,
                    ingredients,
                    stages,
                    outputs
                );
            })
            .ToArray();

        return Ok(recipes);
    }

    [HttpGet("minigames")]
    [ProducesResponseType(typeof(IReadOnlyList<MiniGameDefinition>), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult MiniGames() => Ok(MiniGameCatalog.All);

    private static WikiGameObjectSummary ToPublicSummary(
        IDatabaseObject value,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? questLocationIndex = null
    ) =>
        value switch
        {
            ItemDescriptor item => new WikiGameObjectSummary(
                item.Id,
                item.Name,
                item.Type.ToString(),
                string.IsNullOrWhiteSpace(item.Description) ? item.ItemType.ToString() : item.Description,
                SafeAssetName(item.Icon),
                item.ImageUrl
            ),
            ResourceDescriptor resource => new WikiGameObjectSummary(
                resource.Id,
                resource.Name,
                resource.Type.ToString(),
                "Ressource",
                null,
                resource.ImageUrl
            ),
            SpellDescriptor spell => new WikiGameObjectSummary(
                spell.Id,
                spell.Name,
                spell.Type.ToString(),
                "Sort",
                SafeAssetName(spell.Icon),
                spell.ImageUrl
            ),
            MapDescriptor map => new WikiGameObjectSummary(
                map.Id,
                map.Name,
                map.Type.ToString(),
                $"{map.ZoneType} · {(map.IsIndoors ? "Intérieur" : "Extérieur")}"
            ),
            QuestDescriptor quest => new WikiGameObjectSummary(
                quest.Id,
                quest.Name,
                quest.Type.ToString(),
                QuestSummarySubtitle(quest, questLocationIndex)
            ),

            _ => new WikiGameObjectSummary(value.Id, value.Name, value.Type.ToString())
        };

    private static WikiPublicDetail ToPublicDetail(IDatabaseObject value)
    {
        var facts = new Dictionary<string, object?>();

        switch (value)
        {
            case ItemDescriptor item:
                Add(facts, "Description", item.Description);
                Add(facts, "Catégorie", item.ItemType.ToString());
                Add(facts, "Icône", SafeAssetName(item.Icon));
                Add(facts, "Rareté", item.Rarity);
                Add(facts, "Prix", item.Price);
                Add(facts, "Échangeable", item.CanTrade);
                Add(facts, "Vendable", item.CanSell);
                Add(facts, "Déposable", item.CanDrop);
                Add(facts, "Empilable", item.Stackable);
                if (item.Damage != 0) Add(facts, "Dégâts", item.Damage);
                if (item.CritChance != 0) Add(facts, "Chance critique", item.CritChance);
                if (item.CritMultiplier != 1.5) Add(facts, "Multiplicateur critique", item.CritMultiplier);
                if (item.Cooldown > 0) Add(facts, "Temps de recharge", item.Cooldown);
                if (item.TwoHanded) Add(facts, "Deux mains", true);

                var stats = new Dictionary<string, int>();
                var statNames = Enum.GetNames<Stat>();
                for (var i = 0; i < item.StatsGiven.Length && i < statNames.Length; i++)
                {
                    if (item.StatsGiven[i] != 0)
                    {
                        stats[statNames[i]] = item.StatsGiven[i];
                    }
                }
                if (stats.Count > 0) facts["Statistiques"] = stats;
                break;

            case MapDescriptor map:
                Add(facts, "Zone", map.ZoneType.ToString());
                Add(facts, "Environnement", map.IsIndoors ? "Intérieur" : "Extérieur");
                Add(facts, "Luminosité", map.Brightness);
                break;
        }

        var imageUrl = value switch
        {
            ItemDescriptor item => item.ImageUrl,
            ResourceDescriptor resource => resource.ImageUrl,
            SpellDescriptor spell => spell.ImageUrl,
            _ => null,
        };

        return new WikiPublicDetail(value.Id, value.Name, value.Type.ToString(), imageUrl, facts);
    }

    private static string QuestSummarySubtitle(
        QuestDescriptor quest,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? locationIndex
    )
    {
        var taskCount = quest.Tasks?.Count ?? 0;
        var taskText = taskCount == 1 ? "1 tâche" : $"{taskCount} tâches";

        if (locationIndex != null &&
            locationIndex.TryGetValue(quest.Id, out var locations) &&
            locations.Count > 0)
        {
            return $"{taskText} · {string.Join(", ", locations.Take(2))}";
        }

        return taskText;
    }

    private static string? QuestTaskTarget(QuestTaskDescriptor task) =>
        task.Objective switch
        {
            QuestObjective.GatherItems => ItemDescriptor.GetName(task.TargetId),
            QuestObjective.KillNpcs => NPCDescriptor.GetName(task.TargetId),
            QuestObjective.PotionBrewSpecificRecipe or
            QuestObjective.PotionBrewSpecificRecipeMinScore =>
                string.IsNullOrWhiteSpace(task.TargetName) ? null : task.TargetName,
            _ => string.IsNullOrWhiteSpace(task.TargetName) ? null : task.TargetName,
        };

    private static Dictionary<Guid, IReadOnlyList<string>> BuildQuestLocationIndex()
    {
        var result = new Dictionary<Guid, HashSet<string>>();

        foreach (var eventDescriptor in EventDescriptor.Lookup.Values.OfType<EventDescriptor>())
        {
            if (eventDescriptor.CommonEvent || eventDescriptor.MapId == Guid.Empty)
            {
                continue;
            }

            if (!MapDescriptor.Lookup.TryGetValue(eventDescriptor.MapId, out var mapObject) ||
                mapObject is not MapDescriptor map)
            {
                continue;
            }

            var questIds = QuestIdsStartedByEvent(eventDescriptor, []);
            foreach (var questId in questIds)
            {
                if (!result.TryGetValue(questId, out var locations))
                {
                    locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    result[questId] = locations;
                }

                locations.Add(map.Name);
            }
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        );
    }

    private static HashSet<Guid> QuestIdsStartedByEvent(
        EventDescriptor eventDescriptor,
        HashSet<Guid> visitedCommonEvents
    )
    {
        var result = new HashSet<Guid>();

        foreach (var page in eventDescriptor.Pages ?? [])
        {
            foreach (var command in page.CommandLists.Values.SelectMany(commands => commands))
            {
                switch (command)
                {
                    case StartQuestCommand startQuest when startQuest.QuestId != Guid.Empty:
                        result.Add(startQuest.QuestId);
                        break;

                    case StartCommmonEventCommand startCommon when startCommon.EventId != Guid.Empty:
                        if (!visitedCommonEvents.Add(startCommon.EventId))
                        {
                            break;
                        }

                        if (EventDescriptor.Get(startCommon.EventId) is { CommonEvent: true } common)
                        {
                            result.UnionWith(QuestIdsStartedByEvent(common, visitedCommonEvents));
                        }

                        break;
                }
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ExtractQuestRewards(QuestDescriptor quest)
    {
        var result = new List<string>();
        var descriptions = new[]
        {
            quest.StartDescription,
            quest.InProgressDescription,
            quest.EndDescription,
            quest.BeforeDescription,
        };

        foreach (var description in descriptions)
        {
            foreach (var reward in ExtractRewardLines(description))
            {
                if (!result.Contains(reward, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(reward);
                }
            }
        }

        return result;
    }

    private static IEnumerable<string> ExtractRewardLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var cleaned = Regex.Replace(text, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, "<[^>]+>", " ");
        cleaned = WebUtility.HtmlDecode(cleaned);

        var lines = cleaned
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        var heading = new Regex(
            @"^(?:récompenses?|recompenses?|rewards?)\s*(?::|-|–|—)?\s*(.*)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        for (var i = 0; i < lines.Length; i++)
        {
            var match = heading.Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            var inline = match.Groups[1].Value.Trim();
            if (inline.Length > 0)
            {
                yield return inline;
            }

            for (var j = i + 1; j < lines.Length && j <= i + 6; j++)
            {
                var next = lines[j];
                if (heading.IsMatch(next) ||
                    Regex.IsMatch(
                        next,
                        @"^(?:objectifs?|objectives?|description|histoire|story|mission|quête|quest|tâches?|tasks?|prérequis|requirements?)\s*(?::|-|–|—)",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
                    ))
                {
                    break;
                }

                yield return next;
            }

            yield break;
        }

        foreach (var line in lines)
        {
            if (Regex.IsMatch(
                line,
                @"\b(?:exp|xp|aureons?|logicoins?)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            ) && Regex.IsMatch(line, @"\d"))
            {
                yield return line;
            }
        }
    }

    private static void Add(IDictionary<string, object?> facts, string key, object? value)
    {
        if (value is string text && string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        facts[key] = value;
    }

    private static string? SafeAssetName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var fileName = Path.GetFileName(value.Replace('\\', '/'));
        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        return fileName;
    }
}
