using System.Text.Json;
using Intersect.Framework.Core.Exportation;

namespace Intersect.Server.Exportation;

/// <summary>Server-owned trade route configuration, edited only by authenticated editors.</summary>
internal static class ExportationRuntime
{
    private static readonly object Gate = new();
    private static readonly string FilePath = Path.Combine("resources", "exportation.json");
    private static ExportationConfiguration? _active;

    internal static ExportationConfiguration Current
    {
        get
        {
            lock (Gate) return _active ??= Read();
        }
    }

    private static ExportationConfiguration Read()
    {
        if (!File.Exists(FilePath)) return new ExportationConfiguration { Enabled = false };
        var json = File.ReadAllText(FilePath);
        if (json.Length > 262144) throw new InvalidDataException("Exportation configuration too large.");
        return ExportationConfiguration.FromJson(json);
    }

    internal static string Save(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 262144)
            throw new InvalidDataException("Invalid Exportation configuration size.");
        var candidate = ExportationConfiguration.FromJson(json);
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, candidate.ToJson());
                File.Move(temp, FilePath, overwrite: true);
                _active = candidate;
                return candidate.ToJson();
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }

    internal static ExportationRoute? Find(Guid id) =>
        Current.Enabled ? Current.Routes.FirstOrDefault(route => route.Id == id && route.IsValid) : null;
}
