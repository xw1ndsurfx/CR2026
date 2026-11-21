using Microsoft.Xna.Framework.Content;

namespace Intersect.Client.Android;

// Android's TitleContainer reads APK assets; the game instead uses the extracted files.
internal sealed class FileContentManager(IServiceProvider services) : ContentManager(services)
{
    protected override Stream OpenStream(string assetName) =>
        File.OpenRead(Path.Combine(RootDirectory, assetName + ".xnb"));
}
