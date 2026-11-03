using Intersect.Framework.Core;
using Newtonsoft.Json;
namespace Intersect.Server.PartyLobbies;
internal static class LobbyConfigurationRuntime
{
    private static readonly object Gate = new();
    private static LobbyConfiguration? _current;
    private static string FileName => Path.Combine("resources", "lobbies.json");
    internal static LobbyConfiguration Current
    {
        get { lock(Gate) { if(_current != null)return _current; var value=File.Exists(FileName)?JsonConvert.DeserializeObject<LobbyConfiguration>(File.ReadAllText(FileName))??new():new();value.Validate();return _current=value; } }
    }
    internal static void Save(string json)
    {
        if(json == null || json.Length>32768)throw new InvalidDataException();
        var config=JsonConvert.DeserializeObject<LobbyConfiguration>(json)??throw new InvalidDataException();config.Validate();
        lock(Gate) { Directory.CreateDirectory("resources");File.WriteAllText(FileName+".tmp",JsonConvert.SerializeObject(config));File.Move(FileName+".tmp",FileName,true);_current=config; }
    }
}
