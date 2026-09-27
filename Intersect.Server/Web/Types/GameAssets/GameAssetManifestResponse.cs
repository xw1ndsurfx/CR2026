namespace Intersect.Server.Web.Types.GameAssets;

public sealed record GameAssetImageEntry(
    Guid Id,
    string Name,
    string FileName,
    string? ImageUrl
);

public sealed record GameAssetResourceStateEntry(
    Guid Id,
    string Name,
    string SourceType,
    string FileName,
    int X,
    int Y,
    int Width,
    int Height,
    string? ImageUrl
);

public sealed record GameAssetResourceEntry(
    Guid Id,
    string Name,
    string? ImageUrl,
    GameAssetResourceStateEntry[] States
);

public sealed record GameAssetManifestResponse(
    DateTimeOffset GeneratedAtUtc,
    GameAssetImageEntry[] Items,
    GameAssetResourceEntry[] Resources,
    GameAssetImageEntry[] Spells
);
