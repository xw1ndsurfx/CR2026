using Intersect.Framework.Core.RoyalSeasons;
using Intersect.Network.Packets.RoyalSeasons;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private RoyalSeasonWindow? _royalSeasons;
    internal void HideRoyalSeasons() => _royalSeasons?.Hide();
    internal void OpenRoyalSeasons() => Networking.Network.SendPacket(new RoyalSeasonRequestPacket { Action = SeasonAction.Open });
    internal void ApplyRoyalSeasons(RoyalSeasonStatePacket packet)
    {
        var view = RoyalSeasonRules.Read<RoyalSeasonView>(packet.Json);
        _royalSeasons ??= new RoyalSeasonWindow(GameCanvas);
        _royalSeasons.Apply(view, packet.Open);
    }
}
