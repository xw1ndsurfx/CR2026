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
        var texture = GameContentManager.Current.GetTexture(TextureType.Gui, $"admurin/{file}.png");
        if (texture == null)
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
}
