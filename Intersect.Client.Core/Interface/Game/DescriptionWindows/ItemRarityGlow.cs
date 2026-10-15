using Intersect.Client.Framework.GenericClasses;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;

namespace Intersect.Client.Interface.Game.DescriptionWindows;

internal static class ItemRarityGlow
{
    internal static Color? ResolveColor(
        int rarity,
        IReadOnlyList<string> tiers,
        IReadOnlyDictionary<int, Color> colors
    )
    {
        var rareTier = 3;
        for (var index = 0; index < tiers.Count; ++index)
        {
            if (string.Equals(tiers[index], "Rare", StringComparison.OrdinalIgnoreCase))
            {
                rareTier = index;
                break;
            }
        }

        return rarity >= rareTier && rarity < tiers.Count &&
            colors.TryGetValue(rarity, out var color) && color.A > 0 ? color : null;
    }

    internal static void Draw(RendererBase renderer, int width, int height, Color color, long milliseconds)
    {
        if (width <= 0 || height <= 0 || color.A == 0)
        {
            return;
        }

        // Draw beneath the window, before its content clip, so the halo never covers text.
        var pulse = 0.68 + 0.32 * Math.Sin(milliseconds % 2400 * (2 * Math.PI / 2400));
        var radius = 24;
        var previous = renderer.DrawColor;
        try
        {
            for (var distance = radius; distance >= 1; --distance)
            {
                var falloff = Math.Exp(-Math.Pow(distance / (8.0 + 2 * pulse), 2) / 2);
                var alpha = (int)Math.Round(105 * pulse * falloff * color.A / 255.0);
                if (alpha < 1) continue;
                // The concrete renderer overrides DrawColor; its color-stack helpers do not set it.
                var tint = new Color(alpha, color.R, color.G, color.B);
                renderer.DrawColor = tint;
                renderer.PushDrawColor(tint);
                try
                {
                    var x = -distance;
                    var y = -distance;
                    var outerWidth = width + 2 * distance;
                    var outerHeight = height + 2 * distance;
                    // Non-overlapping strips keep corner brightness consistent with the edges.
                    renderer.DrawFilledRect(new Rectangle(x, y, outerWidth, 1));
                    renderer.DrawFilledRect(new Rectangle(x, height + distance - 1, outerWidth, 1));
                    renderer.DrawFilledRect(new Rectangle(x, y + 1, 1, outerHeight - 2));
                    renderer.DrawFilledRect(new Rectangle(width + distance - 1, y + 1, 1, outerHeight - 2));
                }
                finally { renderer.PopDrawColor(); }
            }
        }
        finally { renderer.DrawColor = previous; }
    }
}
