using System.Net;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Events;
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
        long TimeCreated
    );

    public sealed record WikiCatalogEntry(
        string Slug,
        string Type,
        string Label
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

        return Ok(new
        {
            gameObjects,
            miniGames = MiniGameCatalog.All,
        });
    }

    [HttpGet("gameobjects/{gameObjectType}")]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.BadRequest, ContentTypes.Json)]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.NotFound, ContentTypes.Json)]
    [ProducesResponseType(typeof(DataPage<WikiGameObjectSummary>), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult List(
        GameObjectType gameObjectType,
        [FromQuery] PagingInfo pageInfo
    )
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
            values = values
                .OfType<EventDescriptor>()
                .Where(descriptor => descriptor.CommonEvent);
        }

        var ordered = values.OrderBy(value => value.Name).ToArray();
        var entries = ordered
            .Skip(pageInfo.Page * pageInfo.PageSize)
            .Take(pageInfo.PageSize)
            .Select(value => new WikiGameObjectSummary(
                value.Id,
                value.Name,
                value.Type.ToString(),
                value.TimeCreated
            ))
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
    [ProducesResponseType(typeof(IDatabaseObject), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult Detail(GameObjectType gameObjectType, Guid objectId)
    {
        if (!PublicTypes.Contains(gameObjectType))
        {
            return NotFound($"Game object type '{gameObjectType}' is not published by the wiki API.");
        }

        if (objectId == default)
        {
            return BadRequest($"Invalid id '{objectId}'.");
        }

        if (!gameObjectType.TryGetLookup(out var lookup))
        {
            return BadRequest($"Invalid {nameof(GameObjectType)} '{gameObjectType}'.");
        }

        if (!lookup.TryGetValue(objectId, out var gameObject))
        {
            return NotFound($"No {gameObjectType} with id '{objectId}'.");
        }

        if (gameObjectType == GameObjectType.Event &&
            gameObject is EventDescriptor eventDescriptor &&
            !eventDescriptor.CommonEvent)
        {
            return NotFound($"No published {gameObjectType} with id '{objectId}'.");
        }

        return Ok(gameObject);
    }

    [HttpGet("minigames")]
    [ProducesResponseType(typeof(IReadOnlyList<MiniGameDefinition>), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult MiniGames() => Ok(MiniGameCatalog.All);
}
