using System.Text;
using Intersect.Core;
using Intersect.Framework.Reflection;
using Intersect.IO.Files;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Intersect.Configuration;

public static partial class ConfigurationHelper
{
    public static string CacheName { get; set; } = default!;

    public static T Load<T>(T configuration, string filePath, bool failQuietly = false)
        where T : IConfiguration<T>
    {
        if (!File.Exists(filePath))
        {
            if (failQuietly)
            {
                return configuration;
            }

            throw new FileNotFoundException("Missing configuration file.", filePath);
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using var text = new StreamReader(stream, Encoding.UTF8);
            using var json = new JsonTextReader(text);
            JsonSerializer.CreateDefault().Populate(json, configuration);

            return configuration;
        }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogError(
                exception,
                "Error when reading {ConfigurationType} from {FilePath}",
                typeof(T).GetName(qualified: true),
                filePath
            );

            throw;
        }
    }

    public static T Save<T>(T configuration, string filePath, bool failQuietly = false)
        where T : IConfiguration<T>
    {
        var directoryPath = Path.GetDirectoryName(filePath);
        if (directoryPath == null)
        {
            throw new ArgumentException($"'{filePath}' has no directory name", nameof(filePath));
        }

        if (!FileSystemHelper.EnsureDirectoryExists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory does not exist: {directoryPath}");
        }
        
        var temporary = Path.Combine(Path.GetFullPath(directoryPath), $".config-{Guid.NewGuid():N}.tmp");
        try
        {
            // Stream to a sibling file. A locked target or interrupted write must preserve the previous configuration.
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var text = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n" })
            using (var json = new JsonTextWriter(text) { Formatting = Formatting.Indented })
            {
                JsonSerializer.CreateDefault().Serialize(json, configuration);
                json.Flush(); text.Flush(); stream.Flush(flushToDisk: true);
            }
            if (File.Exists(filePath)) File.Replace(temporary, filePath, null);
            else File.Move(temporary, filePath);
            return configuration;
        }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogError(
                exception,
                "Error when writing {ConfigurationType} to {FilePath}",
                typeof(T).GetName(qualified: true),
                filePath
            );

            throw;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static T LoadSafely<T>(T configuration, string filePath = null)
        where T : IConfiguration<T>
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return configuration;
        }

        // Only a missing file may be initialized from defaults. A failed read must never overwrite existing data.
        if (File.Exists(filePath))
        {
            try { configuration.Load(filePath); }
            catch (Exception exception)
            {
                ApplicationContext.Context.Value?.Logger.LogWarning(exception,
                    "Error when reading {ConfigurationType} from {FilePath}; existing file preserved",
                    typeof(T).GetName(qualified: true), filePath);
                return configuration;
            }
        }
        try { configuration.Save(filePath); }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogError(exception,
                "Error when writing {ConfigurationType} to {FilePath}", typeof(T).GetName(qualified: true), filePath);
        }
        return configuration;
    }
}
