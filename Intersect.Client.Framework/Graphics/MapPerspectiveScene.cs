using System.Numerics;
using Intersect.Client.Framework.GenericClasses;

namespace Intersect.Client.Framework.Graphics;

/// <summary>Read-only, client-visible world data. Coordinates are in native world pixels.</summary>
public sealed record MapPerspectiveScene(Vector2 Camera, Vector2 Forward, float TileWidth, float TileHeight,
    IReadOnlyList<GameTileBuffer> Terrain, IReadOnlyList<MapPerspectiveSprite> Sprites, IReadOnlyList<MapPerspectiveDecoration>? Decorations=null);

public readonly record struct MapPerspectiveObjectKey(Guid Tileset,int X,int Y);
public sealed record MapPerspectiveDecoration(GameTileBuffer Buffer,Vector2 Anchor,MapPerspectiveObjectKey? ObjectKey=null,
    Vector2 ObjectMin=default,Vector2 ObjectMax=default);

public sealed record MapPerspectiveSprite(IGameTexture Texture, FloatRect Source, Vector2 Position,
    float Width, float Height, float AnchorX, float AnchorY, bool FlipHorizontal,float Elevation=0,float Opacity=1);
