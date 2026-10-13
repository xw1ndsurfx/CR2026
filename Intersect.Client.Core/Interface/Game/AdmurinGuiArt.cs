using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Small, passive sprite pieces supplied by Admurin. GUI sprites are kept in a
/// dedicated directory to avoid collisions with player-customized skins.
/// All sprites ignore the mouse so existing buttons remain interactive.
/// </summary>
internal static class AdmurinGuiArt
{
    public static ImagePanel? Add(
        Base parent, string name, string file,
        int x, int y, int width, int height,
        int sourceX = 0, int sourceY = 0, int sourceWidth = 0, int sourceHeight = 0)
    {
        // Packaged builds use gui/admurin; an existing local game may already
        // have the artist sprites directly in resources/gui.
        var manager = GameContentManager.Current;
        var texture = manager.GetTexture(TextureType.Gui, $"admurin/{file}.png") ??
                      manager.GetTexture(TextureType.Gui, $"{file}.png");
        if (texture == null)
            return null;

        // Invalid sprite-sheet coordinates used to silently fall back to the
        // entire image in ImagePanel.SetTextureRect. Never draw a whole sprite
        // sheet in place of a tiny icon when an asset changes.
        if ((sourceWidth > 0 || sourceHeight > 0) &&
            (sourceWidth <= 0 || sourceHeight <= 0 ||
             sourceX < 0 || sourceY < 0 ||
             (long)sourceX + sourceWidth > texture.Width ||
             (long)sourceY + sourceHeight > texture.Height))
            return null;

        var panel = new ImagePanel(parent, name)
        {
            Texture = texture,
            MouseInputEnabled = false,
            ShouldDrawBackground = false,
            MaintainAspectRatio = false,
        };
        if (sourceWidth > 0 && sourceHeight > 0)
            panel.SetTextureRect(sourceX, sourceY, sourceWidth, sourceHeight);
        panel.SetBounds(x, y, width, height);
        return panel;
    }

    /// <summary>
    /// Keeps hand-drawn corners and header thickness intact when a panel is
    /// wider or shorter than the original PNG. Only the straight edges and
    /// center are stretched. All nine sprite pieces ignore pointer input.
    /// </summary>
    public static Base? AddNineSlice(
        Base parent, string name, string file,
        int x, int y, int width, int height,
        int sourceWidth, int sourceHeight,
        int left, int top, int right, int bottom,
        int drawLeft, int drawTop, int drawRight, int drawBottom)
    {
        var manager = GameContentManager.Current;
        var texture = manager.GetTexture(TextureType.Gui, $"admurin/{file}.png") ??
                      manager.GetTexture(TextureType.Gui, $"{file}.png");
        if (texture == null || texture.Width < sourceWidth || texture.Height < sourceHeight)
            return null;

        if (left < 0 || top < 0 || right < 0 || bottom < 0 ||
            drawLeft < 0 || drawTop < 0 || drawRight < 0 || drawBottom < 0 ||
            width <= drawLeft + drawRight || height <= drawTop + drawBottom ||
            sourceWidth <= left + right || sourceHeight <= top + bottom)
            return null;

        var root = new Base(parent, name)
        {
            ShouldDrawBackground = false,
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };
        root.SetBounds(x, y, width, height);

        int[] sourceXs = [0, left, sourceWidth - right];
        int[] sourceYs = [0, top, sourceHeight - bottom];
        int[] sourceWidths = [left, sourceWidth - left - right, right];
        int[] sourceHeights = [top, sourceHeight - top - bottom, bottom];
        int[] destXs = [0, drawLeft, width - drawRight];
        int[] destYs = [0, drawTop, height - drawBottom];
        int[] destWidths = [drawLeft, width - drawLeft - drawRight, drawRight];
        int[] destHeights = [drawTop, height - drawTop - drawBottom, drawBottom];

        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                Add(root, $"{name}_{row}_{col}", file,
                    destXs[col], destYs[row], destWidths[col], destHeights[row],
                    sourceXs[col], sourceYs[row], sourceWidths[col], sourceHeights[row]);
            }
        }

        return root;
    }
}
