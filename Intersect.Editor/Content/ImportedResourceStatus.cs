using System.Security.Cryptography;
using System.Text.Json;

namespace Intersect.Editor.Content;

/// <summary>Content identities survive renamed imports and editor restarts.</summary>
internal static class ImportedResourceStatus
{
    private sealed record Receipt(string SourceHash, string DestinationHash, string Destination);

    private static string Hash(string path, CancellationToken token)
    {
        using var input = File.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        int count;
        while ((count = input.Read(buffer)) > 0)
        {
            token.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, count);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    internal static void Record(string source, string destination, string cache)
    {
        // Status metadata must never turn a successful resource import into a failure.
        try
        {
            Directory.CreateDirectory(cache);
            var receipt = new Receipt(Hash(source, default), Hash(destination, default), Path.GetFullPath(destination));
            File.WriteAllText(Path.Combine(cache, receipt.SourceHash + ".json"), JsonSerializer.Serialize(receipt));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    internal static string? Find(string source, string destinationFolder, string cache, CancellationToken token)
    {
        var sourceHash = Hash(source, token);
        var receiptPath = Path.Combine(cache, sourceHash + ".json");
        if (File.Exists(receiptPath))
        {
            try
            {
                var receipt = JsonSerializer.Deserialize<Receipt>(File.ReadAllText(receiptPath));
                if (receipt != null && File.Exists(receipt.Destination) &&
                    string.Equals(Path.GetDirectoryName(receipt.Destination), Path.GetFullPath(destinationFolder), StringComparison.OrdinalIgnoreCase) &&
                    Hash(receipt.Destination, token) == receipt.DestinationHash)
                    return Path.GetFileName(receipt.Destination);
            }
            catch (JsonException) { } // A damaged cache can be rediscovered below.
        }

        // Also recognize existing copies created before status tracking was available.
        if (!Directory.Exists(destinationFolder)) return null;
        var length = new FileInfo(source).Length;
        foreach (var candidate in Directory.EnumerateFiles(destinationFolder, "*", SearchOption.TopDirectoryOnly))
        {
            token.ThrowIfCancellationRequested();
            if (new FileInfo(candidate).Length == length && Hash(candidate, token) == sourceHash)
                return Path.GetFileName(candidate);
        }
        return null;
    }
}
