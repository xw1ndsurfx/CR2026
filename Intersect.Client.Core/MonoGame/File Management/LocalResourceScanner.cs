using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Graphics;
using Intersect.Core;
using Microsoft.Extensions.Logging;

namespace Intersect.Client.MonoGame.File_Management;
internal static class LocalResourceScanner
{
    // Preserve live objects and packed assets. Index only new loose files, using real
    // on-disk casing for streams and normalized relative paths for lookup keys.
    internal static int AddFiles(string root,string folder,string extension,Dictionary<string,IAsset> lookup,
        Func<string,string,IAsset> create,bool removeExtension,ref int failures,string keyPrefix="",bool recursive=true)
    {
        var directory=Path.Combine(root,folder);var added=0;
        if(!Directory.Exists(directory))return 0;
        try
        {
            foreach(var path in Directory.EnumerateFiles(directory,"*",recursive?SearchOption.AllDirectories:SearchOption.TopDirectoryOnly))
            {
                if(!Path.GetExtension(path).Equals(extension,StringComparison.OrdinalIgnoreCase))continue;
                var key=Path.GetRelativePath(directory,path).Replace('\\','/').ToLowerInvariant();
                if(removeExtension)key=key[..^extension.Length];
                key=keyPrefix+key;
                if(lookup.TryGetValue(key,out var existing))
                {
                    if(existing is IGameTexture {IsMissingOrCorrupt:true} missing)
                    {
                        try{missing.Reload();if(missing.IsMissingOrCorrupt)failures++;else added++;}
                        catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidOperationException)
                        {failures++;ApplicationContext.Context.Value?.Logger.LogWarning(error,"Unable to retry missing texture {ResourcePath}",path);}
                    }
                    continue;
                }
                try{lookup.Add(key,create(key,path));added++;}
                catch(Exception error) when(error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
                {failures++;ApplicationContext.Context.Value?.Logger.LogWarning(error,"Unable to register local resource {ResourcePath}",path);}
            }
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException)
        {failures++;ApplicationContext.Context.Value?.Logger.LogWarning(error,"Unable to scan resource directory {ResourceDirectory}",directory);}
        return added;
    }
}
