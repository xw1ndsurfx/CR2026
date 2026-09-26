using System.Net;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.MiniGames.Configuration;
using Intersect.Models;
using Intersect.Server.Web.Http;
using Intersect.Server.Web.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        string? Icon = null
    );

    public sealed record WikiCatalogEntry(string Slug, string Type, string Label);

    public sealed record WikiPublicDetail(
        Guid Id,
        string Name,
        string Type,
        IReadOnlyDictionary<string, object?> Facts
    );

    public sealed record WikiMapLayoutEntry(
        Guid Id,
        string Name,
        int X,
        int Y,
        bool IsIndoors,
        string Zone
    );

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
        var entries = ordered
            .Skip(pageInfo.Page * pageInfo.PageSize)
            .Take(pageInfo.PageSize)
            .Select(ToPublicSummary)
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
                map.ZoneType.ToString()
            ));
        }

        return Ok(result);
    }

    [HttpGet("minigames")]
    [ProducesResponseType(typeof(IReadOnlyList<MiniGameDefinition>), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult MiniGames() => Ok(MiniGameCatalog.All);

    private static WikiGameObjectSummary ToPublicSummary(IDatabaseObject value) =>
        value switch
        {
            ItemDescriptor item => new WikiGameObjectSummary(
                item.Id,
                item.Name,
                item.Type.ToString(),
                string.IsNullOrWhiteSpace(item.Description) ? item.ItemType.ToString() : item.Description,
                SafeAssetName(item.Icon)
            ),
            MapDescriptor map => new WikiGameObjectSummary(
                map.Id,
                map.Name,
                map.Type.ToString(),
                $"{map.ZoneType} · {(map.IsIndoors ? "Intérieur" : "Extérieur")}"
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

        return new WikiPublicDetail(value.Id, value.Name, value.Type.ToString(), facts);
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
