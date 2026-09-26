using System.Net;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.GameObjects;
using Intersect.Server.Web.Http;
using Intersect.Server.Web.Types;
using Intersect.Server.Web.Types.GameAssets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using ImageSharpRectangle = SixLabors.ImageSharp.Rectangle;

namespace Intersect.Server.Web.Controllers.Api.V1;

/// <summary>
/// Public, read-only delivery surface for the game art that is intentionally safe
/// to expose to the player wiki. It only resolves files referenced by real
/// item/resource/spell descriptors and never accepts an arbitrary filesystem path.
/// </summary>
[Authorize]
[Route("api/v1/game-assets")]
public sealed class GameAssetController(ILogger<GameAssetController> logger) : IntersectController
{
    private static readonly string[] AssetRoots =
    [
        Path.Combine("assets", "editor", "resources"),
        Path.Combine("assets", "client", "resources"),
        "resources",
    ];

    private readonly DirectoryInfo _cacheRoot =
        new(Path.Combine(Environment.CurrentDirectory, ".cache", "game-assets"));

    [HttpGet("manifest")]
    [ProducesResponseType(typeof(GameAssetManifestResponse), (int)HttpStatusCode.OK, ContentTypes.Json)]
    public IActionResult Manifest()
    {
        var items = ItemDescriptor.Lookup.Values
            .OfType<ItemDescriptor>()
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new GameAssetImageEntry(
                item.Id,
                item.Name,
                item.Icon ?? string.Empty,
                string.IsNullOrWhiteSpace(item.Icon)
                    ? null
                    : AbsoluteUrl($"/api/v1/game-assets/items/{item.Id:D}")
            ))
            .ToArray();

        var resources = ResourceDescriptor.Lookup.Values
            .OfType<ResourceDescriptor>()
            .OrderBy(resource => resource.Name, StringComparer.OrdinalIgnoreCase)
            .Select(resource =>
            {
                var states = (resource.States?.Values.AsEnumerable() ?? Enumerable.Empty<ResourceStateDescriptor>())
                    .Where(state => state != null)
                    .OrderByDescending(state => state.MaximumHealth)
                    .ThenByDescending(state => state.MinimumHealth)
                    .Select(state => new GameAssetResourceStateEntry(
                        state.Id,
                        state.Name ?? string.Empty,
                        state.TextureType.ToString(),
                        ResourceStateFileName(state),
                        state.X,
                        state.Y,
                        state.Width,
                        state.Height,
                        AbsoluteUrl($"/api/v1/game-assets/resources/{resource.Id:D}/{state.Id:D}")
                    ))
                    .ToArray();

                return new GameAssetResourceEntry(
                    resource.Id,
                    resource.Name,
                    states.Length == 0
                        ? null
                        : AbsoluteUrl($"/api/v1/game-assets/resources/{resource.Id:D}"),
                    states
                );
            })
            .ToArray();

        var spells = SpellDescriptor.Lookup.Values
            .OfType<SpellDescriptor>()
            .OrderBy(spell => spell.Name, StringComparer.OrdinalIgnoreCase)
            .Select(spell => new GameAssetImageEntry(
                spell.Id,
                spell.Name,
                spell.Icon ?? string.Empty,
                string.IsNullOrWhiteSpace(spell.Icon)
                    ? null
                    : AbsoluteUrl($"/api/v1/game-assets/spells/{spell.Id:D}")
            ))
            .ToArray();

        Response.Headers[HeaderNames.CacheControl] = "private,no-store";
        return Ok(new GameAssetManifestResponse(DateTimeOffset.UtcNow, items, resources, spells));
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    [HttpGet("items/{itemId:guid}")]
    [ProducesResponseType(typeof(byte[]), (int)HttpStatusCode.OK, ContentTypes.Png)]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.NotFound, ContentTypes.Json)]
    public IActionResult Item(Guid itemId)
    {
        if (!ItemDescriptor.TryGet(itemId, out var item) || string.IsNullOrWhiteSpace(item.Icon))
            return NotFound("Item image not found.");

        var file = ResolveTextureFile("items", item.Icon);
        return file == null ? NotFound("Item image file not found.") : Png(file);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    [HttpGet("spells/{spellId:guid}")]
    [ProducesResponseType(typeof(byte[]), (int)HttpStatusCode.OK, ContentTypes.Png)]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.NotFound, ContentTypes.Json)]
    public IActionResult Spell(Guid spellId)
    {
        if (!SpellDescriptor.TryGet(spellId, out var spell) || string.IsNullOrWhiteSpace(spell.Icon))
            return NotFound("Spell image not found.");

        var file = ResolveTextureFile("spells", spell.Icon);
        return file == null ? NotFound("Spell image file not found.") : Png(file);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    [HttpGet("resources/{resourceId:guid}")]
    [ProducesResponseType(typeof(byte[]), (int)HttpStatusCode.OK, ContentTypes.Png)]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.NotFound, ContentTypes.Json)]
    public IActionResult Resource(Guid resourceId)
    {
        if (!ResourceDescriptor.TryGet(resourceId, out var resource))
            return NotFound("Resource not found.");

        var state = PrimaryState(resource);
        if (state == null)
            return NotFound("Resource image not found.");

        var file = ResolveResourceStateImage(resource, state);
        return file == null ? NotFound("Resource image file not found.") : Png(file);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    [HttpGet("resources/{resourceId:guid}/{stateId:guid}")]
    [ProducesResponseType(typeof(byte[]), (int)HttpStatusCode.OK, ContentTypes.Png)]
    [ProducesResponseType(typeof(StatusMessageResponseBody), (int)HttpStatusCode.NotFound, ContentTypes.Json)]
    public IActionResult ResourceState(Guid resourceId, Guid stateId)
    {
        if (!ResourceDescriptor.TryGet(resourceId, out var resource) ||
            resource.States == null ||
            !resource.States.TryGetValue(stateId, out var state) ||
            state == null)
        {
            return NotFound("Resource state image not found.");
        }

        var file = ResolveResourceStateImage(resource, state);
        return file == null ? NotFound("Resource state image file not found.") : Png(file);
    }

    private IActionResult Png(FileInfo file)
    {
        Response.Headers[HeaderNames.CacheControl] = "public,max-age=3600";
        Response.Headers[HeaderNames.LastModified] = file.LastWriteTimeUtc.ToString("R");
        return new PhysicalFileResult(file.FullName, ContentTypes.Png)
        {
            EnableRangeProcessing = true,
        };
    }

    private string AbsoluteUrl(string path)
    {
        var prefix = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        return prefix.TrimEnd('/') + "/" + path.TrimStart('/');
    }

    private static ResourceStateDescriptor? PrimaryState(ResourceDescriptor resource) =>
        (resource.States?.Values.AsEnumerable() ?? Enumerable.Empty<ResourceStateDescriptor>())
            .Where(state => state != null)
            .OrderByDescending(state => state.MaximumHealth)
            .ThenByDescending(state => state.MinimumHealth)
            .FirstOrDefault();

    private FileInfo? ResolveResourceStateImage(
        ResourceDescriptor resource,
        ResourceStateDescriptor state
    )
    {
        var source = ResolveResourceSource(state, out var crop);
        if (source == null)
            return null;

        if (crop == null)
            return source;

        var cacheFile = new FileInfo(
            Path.Combine(
                _cacheRoot.FullName,
                "resources",
                resource.Id.ToString("N"),
                state.Id.ToString("N") + ".png"
            )
        );

        try
        {
            if (cacheFile.Exists && cacheFile.LastWriteTimeUtc >= source.LastWriteTimeUtc)
                return cacheFile;

            cacheFile.Directory?.Create();

            using var image = Image.Load(source.FullName);
            var rectangle = crop.Value;

            var x = Math.Clamp(rectangle.X, 0, Math.Max(0, image.Width - 1));
            var y = Math.Clamp(rectangle.Y, 0, Math.Max(0, image.Height - 1));
            var width = Math.Clamp(rectangle.Width, 1, image.Width - x);
            var height = Math.Clamp(rectangle.Height, 1, image.Height - y);

            image.Mutate(context => context.Crop(new ImageSharpRectangle(x, y, width, height)));
            image.SaveAsPng(cacheFile.FullName);
            return cacheFile;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to prepare public resource image {ResourceId}/{StateId} from {SourceFile}",
                resource.Id,
                state.Id,
                source.FullName
            );
            return null;
        }
    }

    private FileInfo? ResolveResourceSource(
        ResourceStateDescriptor state,
        out ImageSharpRectangle? crop
    )
    {
        crop = null;

        switch (state.TextureType)
        {
            case ResourceTextureSource.Resource:
            {
                var source = ResolveTextureFile("resources", state.TextureName);
                crop = StateCrop(state);
                return source;
            }

            case ResourceTextureSource.Tileset:
            {
                var source = ResolveTextureFile("tilesets", state.TextureName);
                crop = StateCrop(state);
                return source;
            }

            case ResourceTextureSource.Animation:
            {
                var animation = AnimationDescriptor.Get(state.AnimationId);
                if (animation == null)
                    return null;

                var layer = !string.IsNullOrWhiteSpace(animation.Lower?.Sprite)
                    ? animation.Lower
                    : animation.Upper;

                if (layer == null || string.IsNullOrWhiteSpace(layer.Sprite))
                    return null;

                var source = ResolveTextureFile("animations", layer.Sprite);
                if (source == null)
                    return null;

                try
                {
                    var info = Image.Identify(source.FullName);
                    if (info == null)
                        return source;

                    var xFrames = Math.Max(1, layer.XFrames);
                    var yFrames = Math.Max(1, layer.YFrames);
                    crop = new ImageSharpRectangle(
                        0,
                        0,
                        Math.Max(1, info.Width / xFrames),
                        Math.Max(1, info.Height / yFrames)
                    );
                }
                catch
                {
                    crop = null;
                }

                return source;
            }

            default:
                return null;
        }
    }

    private static ImageSharpRectangle? StateCrop(ResourceStateDescriptor state)
    {
        if (state.Width <= 0 || state.Height <= 0)
            return null;

        return new ImageSharpRectangle(
            Math.Max(0, state.X),
            Math.Max(0, state.Y),
            state.Width,
            state.Height
        );
    }

    private FileInfo? ResolveTextureFile(string category, string? configuredName)
    {
        if (string.IsNullOrWhiteSpace(configuredName))
            return null;

        var normalized = configuredName
            .Replace('\\', '/')
            .Trim()
            .TrimStart('/');

        if (Path.GetExtension(normalized).Length == 0)
            normalized += ".png";

        if (!string.Equals(Path.GetExtension(normalized), ".png", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var assetRoot in AssetRoots)
        {
            var root = new DirectoryInfo(Path.GetFullPath(assetRoot));
            if (!root.Exists)
                continue;

            var categoryRoot = new DirectoryInfo(Path.Combine(root.FullName, category));
            if (!categoryRoot.Exists)
                continue;

            var candidatePath = Path.GetFullPath(
                Path.Combine(
                    categoryRoot.FullName,
                    normalized.Replace('/', Path.DirectorySeparatorChar)
                )
            );

            var relative = Path.GetRelativePath(categoryRoot.FullName, candidatePath);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && File.Exists(candidatePath))
                return new FileInfo(candidatePath);

            var fileName = Path.GetFileName(normalized);
            try
            {
                var fallback = categoryRoot
                    .EnumerateFiles("*.png", SearchOption.AllDirectories)
                    .FirstOrDefault(file =>
                        string.Equals(file.Name, fileName, StringComparison.OrdinalIgnoreCase));

                if (fallback != null)
                    return fallback;
            }
            catch (Exception exception)
            {
                logger.LogDebug(
                    exception,
                    "Unable to enumerate public game asset category {CategoryRoot}",
                    categoryRoot.FullName
                );
            }
        }

        return null;
    }

    private static string ResourceStateFileName(ResourceStateDescriptor state)
    {
        if (state.TextureType != ResourceTextureSource.Animation)
            return state.TextureName ?? string.Empty;

        var animation = AnimationDescriptor.Get(state.AnimationId);
        if (animation == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(animation.Lower?.Sprite)
            ? animation.Lower.Sprite
            : animation.Upper?.Sprite ?? string.Empty;
    }
}
