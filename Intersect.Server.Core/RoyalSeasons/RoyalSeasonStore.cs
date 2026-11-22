using System.Security.Cryptography;
using System.Text;
using Intersect.Framework.Core.RoyalSeasons;

namespace Intersect.Server.RoyalSeasons;

internal sealed class RoyalSeasonStore(string path)
{
    internal readonly object Gate = new();
    private RoyalSeasonConfiguration? _configuration;
    private bool _corrupt;
    internal RoyalSeasonConfiguration Configuration
    {
        get
        {
            if (Volatile.Read(ref _configuration) is {} cached) return cached;
            lock (Gate)
            {
                if (_configuration != null) return _configuration;
                try { return _configuration = File.Exists(path) ? Read(File.ReadAllText(path)) : new(); }
                catch { _corrupt = true; return _configuration = new(); }
            }
        }
    }
    internal string Revision => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RoyalSeasonRules.Json(Configuration))));
    internal static RoyalSeasonConfiguration Read(string json)
    {
        if (json == null || json.Length > 2000000) throw new InvalidOperationException("Invalid season configuration.");
        var value = RoyalSeasonRules.Read<RoyalSeasonConfiguration>(json);
        if (!value.IsValid) throw new InvalidOperationException("Invalid season configuration.");
        return value;
    }
    internal void Save(string json, string revision, DateTimeOffset now, Action<RoyalSeasonConfiguration> validate)
    {
        lock (Gate)
        {
            if (revision != Revision) throw new InvalidOperationException("The configuration changed. Reopen the editor before saving.");
            if (_corrupt) throw new InvalidOperationException("The saved season file is unreadable. Restore it before saving.");
            var next = Read(json);
            foreach (var old in Configuration.Seasons.Where(s => s.Enabled && s.StartsAt <= now))
            {
                var replacement = next.Seasons.SingleOrDefault(s => s.Id == old.Id);
                if (replacement == null || RoyalSeasonRules.Json(old) != RoyalSeasonRules.Json(replacement))
                    throw new InvalidOperationException("Started seasons are locked. Create a new season; pause using the global switch.");
            }
            validate(next);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, Newtonsoft.Json.JsonConvert.SerializeObject(next, Newtonsoft.Json.Formatting.Indented));
            File.Move(temp, path, true);
            Volatile.Write(ref _configuration, next);
        }
    }
}
