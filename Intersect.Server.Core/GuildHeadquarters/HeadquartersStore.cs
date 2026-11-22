using System.Security.Cryptography;
using System.Text;
using Intersect.Framework.Core.GuildHeadquarters;
using Newtonsoft.Json;

namespace Intersect.Server.GuildHeadquarters;

internal sealed class HeadquartersStore(string path)
{
    internal readonly object Gate = new();
    private HeadquartersConfiguration? _configuration;
    internal HeadquartersConfiguration Configuration
    {
        get
        {
            if (Volatile.Read(ref _configuration) is {} cached) return cached;
            lock (Gate)
            {
                if (_configuration != null) return _configuration;
                try
                {
                    var value = Read(File.ReadAllText(path));
                    return _configuration = value.IsValid(Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight) ? value : new();
                }
                catch { return _configuration = new(); } // Disabled on a corrupt file; never overwrite it on startup.
            }
        }
    }
    internal string Revision => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Configuration))));
    internal static HeadquartersConfiguration Read(string json)
    {
        if (json == null || json.Length > 100000) throw new InvalidOperationException("Invalid headquarters configuration.");
        return JsonConvert.DeserializeObject<HeadquartersConfiguration>(json, new JsonSerializerSettings { MaxDepth = 12, TypeNameHandling = TypeNameHandling.None })
            ?? throw new InvalidOperationException("Invalid headquarters configuration.");
    }
    internal void Save(string json, string revision, Action<HeadquartersConfiguration> validate)
    {
        lock (Gate)
        {
            if (revision != Revision) throw new InvalidOperationException("The configuration changed. Reopen the editor before saving.");
            var next = Read(json);
            if (!next.IsValid(Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight)) throw new InvalidOperationException("Invalid headquarters configuration.");
            var previous = Configuration;
            if (next.Tiers.Length < previous.Tiers.Length || previous.Tiers.Where((t, i) => next.Tiers[i].Id != t.Id).Any())
                throw new InvalidOperationException("Keep saved tiers in order. You can append new tiers.");
            validate(next);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(next, Formatting.Indented));
            File.Move(temp, path, true);
            Volatile.Write(ref _configuration, next);
        }
    }
}
