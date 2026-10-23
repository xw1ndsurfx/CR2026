namespace Intersect.Framework.Core.Travel;
public sealed class TeleportConfiguration
{
    public bool FriendsEnabled {get;set;}
    public bool LobbyEnabled {get;set;}=true;
    public bool RestrictLobbyMaps {get;set;}
    public bool CheckOriginMap {get;set;}=true;
    public Guid[] AllowedMaps {get;set;}=[];
    public Guid AnimationId {get;set;}
    public int CooldownSeconds {get;set;}=15;
    public bool Allows(Guid origin,Guid destination,bool friend)=>
        (friend?FriendsEnabled:LobbyEnabled) && (!friend && !RestrictLobbyMaps || AllowedMaps.Contains(destination) && (!CheckOriginMap || AllowedMaps.Contains(origin)));
    public bool IsValid=>AllowedMaps is {Length:<=10000} && AllowedMaps.All(map=>map!=Guid.Empty) && AllowedMaps.Distinct().Count()==AllowedMaps.Length && CooldownSeconds is >=0 and <=3600;
}
