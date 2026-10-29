using Intersect.Framework.Core.RoyalContracts;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private RoyalContractsWindow? _royalContractsWindow;
    public void HideRoyalContracts() => _royalContractsWindow?.Hide();
    public void ApplyRoyalContractsState(RoyalContractBoardPacket packet)
    {
        if (!packet.OpenWindow && (_royalContractsWindow == null || _royalContractsWindow.IsHidden)) return;
        if (packet.OpenWindow) GameMenu?.HideWindows();
        _royalContractsWindow ??= new RoyalContractsWindow(GameCanvas);
        _royalContractsWindow.Apply(RoyalContractRules.Read<RoyalContractBoardState>(packet.StateJson));
        if (packet.OpenWindow) { _royalContractsWindow.Show(); _royalContractsWindow.BringToFront(); }
    }
}
