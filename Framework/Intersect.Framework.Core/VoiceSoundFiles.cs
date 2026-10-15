namespace Intersect;

public static class VoiceSoundFiles
{
    public const string Folder = "voicesound";
    public static string[] List(string resources) => Directory.Exists(Path.Combine(resources, Folder))
        ? Directory.GetFiles(Path.Combine(resources, Folder), "*.wav").Select(Path.GetFileName).OfType<string>().OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray()
        : [];

    public static string? Key(string? file)
    {
        if (string.IsNullOrWhiteSpace(file) || file.IndexOfAny(['/', '\\', ':']) >= 0 || file.Contains("..") ||
            !file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) return null;
        return Folder + "/" + Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
    }
}
