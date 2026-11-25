using System.Numerics;
using Intersect.Enums;
using Intersect.Client.Framework.Entities;

namespace Intersect.Client.Entities;

public partial class Entity
{
    private Vector2? _navalDeckOrigin;
    private bool _deckWalking;

    // Reuse the normal body, cosmetics, transformation and equipment renderer.
    // No network coordinates or movement state are changed for a ship draw.
    internal void DrawOnShip(float x, float y, Direction facing,float scale=1,bool walking=false)
    {
        if(IsHidden || IsStealthed && this!=Intersect.Client.General.Globals.Me && Intersect.Client.General.Globals.Me?.IsInMyParty(Id)!=true)return;
        var origin = _navalDeckOrigin;
        var direction = _directionFacing;
        var lastDirection = _lastDirection;
        var bounds = WorldPos;
        var top = RenderedGraphicTop;
        var anchor = _spriteAnchorOffset;
        var seated = JoustSeated;
        var animation=SpriteAnimation;
        var wasWalking=_deckWalking;
        try
        {
            _navalDeckOrigin = new Vector2(x,y);
            _deckWalking=walking;
            SpriteAnimation=SpriteAnimations.Normal;
            JoustSeated = false;
            _directionFacing = _lastDirection = facing;
            using var spriteScale=Intersect.Client.Core.Graphics.ScaleSprites(x,y,scale);
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
            SpriteAnimation=animation;
            _deckWalking=wasWalking;
        }
    }
}
