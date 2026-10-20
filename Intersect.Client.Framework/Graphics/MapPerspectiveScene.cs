using System.Numerics;
using Intersect.Client.Framework.GenericClasses;

namespace Intersect.Client.Framework.Graphics;

/// <summary>Read-only, client-visible world data. Coordinates are in native world pixels.</summary>
public sealed record MapPerspectiveScene(Vector2 Camera, Vector2 Forward, float TileWidth, float TileHeight,
    IReadOnlyList<GameTileBuffer> Terrain, IReadOnlyList<MapPerspectiveSprite> Sprites);

public sealed record MapPerspectiveSprite(IGameTexture Texture, FloatRect Source, Vector2 Position,
    float Width, float Height, float AnchorX, float AnchorY, bool FlipHorizontal);
