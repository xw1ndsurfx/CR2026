using Intersect.Editor.Content;

namespace Intersect.Editor.Forms.Styling;

internal static class EditorResourcePreview
{
    private static readonly string[] Folders = ["entities", "items", "spells", "animations", "paperdolls", "faces", "images", "fogs", "resources", "tilesets", "gui", "misc", "ocean", "sounds", "music", "voices"];
    internal static bool IsImage(string extension) => extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp";

    internal static string? ResolveFile(string name, string? directory)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name)) return null;
        try
        {
            string? InDirectory(string folder)
            {
                var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var path = Path.GetFullPath(Path.Combine(root, name));
                return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? path : null;
            }
            if (directory != null) return InDirectory(directory);
            var paths = Folders.Select(folder => InDirectory(Path.Combine("resources", folder))).OfType<string>()
                .Concat(GameContentManager.AllTextures.Select(texture => texture.GetPath())
                    .Where(path => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                    .Select(Path.GetFullPath))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
            // Identical names can mean different images in different resource categories.
            return paths.Length == 1 ? paths[0] : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return null; }
    }

    internal static Bitmap? LoadImage(string path, int maximumDimension = 2048)
    {
        try
        {
            if (new FileInfo(path).Length > 32 * 1024 * 1024) return null;
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var source = Image.FromStream(stream);
            if ((long)source.Width * source.Height > 32_000_000) return null;
            var scale = Math.Min(1d, (double)maximumDimension / Math.Max(source.Width, source.Height));
            return new Bitmap(source, new Size(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale))));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException or System.Runtime.InteropServices.ExternalException)
        { return null; }
    }
}
