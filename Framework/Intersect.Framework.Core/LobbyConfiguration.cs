using Intersect.Network.Packets.PartyLobbies;
namespace Intersect.Framework.Core;
public sealed class LobbyPresentation
{
    public LobbyActivity Activity { get; set; }
    public string Image { get; set; } = "";
    public int MaximumMembers { get; set; } = 100;
}
public sealed class LobbyConfiguration
{
    public List<LobbyPresentation> Activities { get; set; } = [];
    public void Validate()
    {
        if (Activities == null || Activities.Count > Enum.GetValues<LobbyActivity>().Length || Activities.Any(a => a == null) || Activities.Select(a => a.Activity).Distinct().Count() != Activities.Count) throw new InvalidDataException();
        foreach(var a in Activities)
            if (!Enum.IsDefined(a.Activity) || a.MaximumMembers is < 1 or > 100 || a.Image == null || a.Image.Length > 160 || a.Image.StartsWith('/') || a.Image.StartsWith('\\') || a.Image.Contains(':') || a.Image.Contains("..") || a.Image.Any(char.IsControl)) throw new InvalidDataException();
    }
}
