using System.Text.Json;

namespace Intersect.Editor.Content;
internal static class ImportedResourceDeclarations
{
    private sealed record Declaration(bool Imported);
    internal static bool? Read(string source,string cache,CancellationToken token=default)
    {
        var path=Path.Combine(cache,ImportedResourceStatus.Hash(source,token)+".declared.json");
        if(!File.Exists(path))return null;
        try{return JsonSerializer.Deserialize<Declaration>(File.ReadAllText(path))?.Imported;}
        catch(JsonException){return null;}
    }
    internal static void Write(string source,string cache,bool imported)
    {
        var hash=ImportedResourceStatus.Hash(source,default);Directory.CreateDirectory(cache);
        var path=Path.Combine(cache,hash+".declared.json");var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporary,JsonSerializer.Serialize(new Declaration(imported)));File.Move(temporary,path,true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
