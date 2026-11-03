using Intersect.Client.Framework.GenericClasses;

namespace Intersect.Client.Entities;

public partial class Resource
{
    private void CaptureFallingTree()
    {
        if (_fallTexture != null || Descriptor?.FallingTree != true || Texture == null ||
            _renderBoundsSrc.Width <= 0 || _renderBoundsSrc.Height <= 0 || _renderBoundsDest.Height <= 0) return;
        ClearFallingSprite();
        _fallTexture = Texture; _fallSource = _renderBoundsSrc; _fallDestination = _renderBoundsDest;
        _fallStarted = Environment.TickCount64;
    }

    internal FloatRect ResourceVisibilityBounds
    {
        get
        {
            if (_fallTexture == null) return _renderBoundsDest;
            if (Environment.TickCount64 - _fallStarted >= 1100)
            {
                _fallTexture = null; ClearFallingSprite(); return _renderBoundsDest;
            }
            // Include the standing sprite and its sweep even if the exhausted
            // state has no graphic or a tiny stump outside the viewport.
            var reach = Math.Max(_fallDestination.Width, _fallDestination.Height);
            return new FloatRect(_fallDestination.X-reach, _fallDestination.Y-reach,
                _fallDestination.Width+reach*2, _fallDestination.Height+reach*2);
        }
    }
}
