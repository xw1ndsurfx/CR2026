using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
namespace Intersect.Client.Core;

internal static class AppearancePaperdolls
{
    internal static string Resolve(string style, bool female)
    {
        if (string.IsNullOrEmpty(style)) return style;
        var variant = Path.GetFileNameWithoutExtension(style) + (female ? "_female.png" : "_male.png");
        return GameContentManager.Current.GetTexture(TextureType.Paperdoll, variant) != null ? variant : style;
    }
}
