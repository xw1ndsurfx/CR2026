using System.IO.Compression;
namespace Intersect.Editor.Content;

internal static class CharacterArchiveImporter
{
    internal static (int Imported, int Skipped, int Invalid) Import(string zip, string root, IEnumerable<string> categories)
    {
        var known = categories.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(n => n, n => n, StringComparer.OrdinalIgnoreCase);
        using var archive = ZipFile.OpenRead(zip);
        var imported = 0; var skipped = 0; var invalid = 0; long bytes = 0;
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            var pieces = entry.FullName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Any(p => p is "." or ".." || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) || entry.Length > 64 * 1024 * 1024 || (bytes += entry.Length) > 1024L * 1024 * 1024) { invalid++; continue; }
            var index = Array.FindLastIndex(pieces[..^1], p => known.ContainsKey(p));
            var category = index >= 0 ? known[pieces[index]] : Path.GetFileNameWithoutExtension(pieces[^1]).Split('_').FirstOrDefault(p => known.ContainsKey(p));
            if (category == null) { invalid++; continue; }
            category = known[category];
            if (category.Equals("Body", StringComparison.OrdinalIgnoreCase) && !Path.GetFileNameWithoutExtension(pieces[^1]).Split('_').Contains("Ape", StringComparer.OrdinalIgnoreCase)) category = "Base";
            var gender = pieces.FirstOrDefault(p => p.Equals("Female", StringComparison.OrdinalIgnoreCase) || p.Equals("Male", StringComparison.OrdinalIgnoreCase));
            var relative = index >= 0 ? pieces[(index + 1)..] : new[] { pieces[^1] };
            var directory = gender == null ? Path.Combine(root, category) : Path.Combine(root, category, gender);
            var destination = Path.GetFullPath(Path.Combine(new[] { directory }.Concat(relative).ToArray()));
            var safeRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase)) { invalid++; continue; }
            if (File.Exists(destination)) { skipped++; continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var temporary = destination + ".importing";
            try { entry.ExtractToFile(temporary, true); using (var bitmap = new System.Drawing.Bitmap(temporary)) { if (bitmap.Width > 16384 || bitmap.Height > 16384) throw new InvalidDataException("Sprite too large."); } File.Move(temporary, destination); imported++; }
            catch { invalid++; }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return (imported, skipped, invalid);
    }
}
