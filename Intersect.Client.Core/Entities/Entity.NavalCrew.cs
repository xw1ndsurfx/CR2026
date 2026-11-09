using System.Numerics;
using Intersect.Enums;

namespace Intersect.Client.Entities;

public partial class Entity
{
    private Vector2? _navalDeckOrigin;

    // Reuse the normal body, cosmetics, transformation and equipment renderer.
    // No network coordinates or movement state are changed for a ship draw.
    internal void DrawOnShip(float x, float y, Direction facing)
    {
        var origin = _navalDeckOrigin;
        var direction = _directionFacing;
        var lastDirection = _lastDirection;
        var bounds = WorldPos;
        var top = RenderedGraphicTop;
        var anchor = _spriteAnchorOffset;
        var seated = JoustSeated;
        try
        {
            _navalDeckOrigin = new Vector2(x,y);
            JoustSeated = false;
            _directionFacing = _lastDirection = facing;
            Draw();
        }
        finally
        {
            _navalDeckOrigin = origin;
            _directionFacing = direction;
            _lastDirection = lastDirection;
            WorldPos = bounds;
            RenderedGraphicTop = top;
            _spriteAnchorOffset = anchor;
            JoustSeated = seated;
        }
    }
}
