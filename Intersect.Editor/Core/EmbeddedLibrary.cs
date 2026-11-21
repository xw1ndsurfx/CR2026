namespace Intersect.Editor.Core;

internal static class EmbeddedLibrary
{
    internal static void EnsureExtracted(string path, Stream resource)
    {
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        var expected = buffer.ToArray();
        // Loaded DLLs can be read but cannot be overwritten on Windows.
        // Leave an identical library untouched when another editor is running.
        bool Matches()
        {
            if (!File.Exists(path)) return false;
            using var existing = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (existing.Length != expected.Length) return false;
            using var actual = new MemoryStream();
            existing.CopyTo(actual);
            return actual.ToArray().AsSpan().SequenceEqual(expected);
        }
        if (Matches()) return;

        var staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(staging, expected);
            try { File.Move(staging, path, overwrite: true); }
            catch (IOException) when (Matches())
            {
                // Another process finished extracting the same library first.
            }
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }
}
