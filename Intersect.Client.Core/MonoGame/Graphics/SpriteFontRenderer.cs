using Intersect.Client.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics;

namespace Intersect.Client.MonoGame.Graphics;

public sealed class SpriteFontRenderer(
    SpriteFont platformObject,
    float renderScale = 1f
) : FontSizeRenderer<SpriteFont>(platformObject)
{
    public float RenderScale { get; } = renderScale;
}
