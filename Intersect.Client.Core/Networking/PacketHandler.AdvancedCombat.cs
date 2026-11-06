using Intersect.Client.Core;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, CombatTelegraphPacket packet)
    {
        Graphics.ReceiveAdvancedCombatTelegraph(packet);
        if (packet.EnrageStarted)
            Intersect.Client.Interface.Interface.EnqueueInGame(gameInterface =>
                gameInterface.NotifyAdvancedBossEnrage());

        if (packet.ComboTotal is >= 2 and <= 3 && packet.ComboStep >= 1 &&
            packet.ComboStep <= packet.ComboTotal)
            Intersect.Client.Interface.Interface.EnqueueInGame(gameInterface =>
            {
                if (Intersect.Client.General.Globals.Me?.MapId == packet.MapId)
                    gameInterface.NotifyAdvancedBossCombo(packet.ComboStep, packet.ComboTotal);
            });
    }
    
    public void HandlePacket(IPacketSender sender, AdvancedCombatStatePacket packet) =>
        Intersect.Client.Interface.Interface.EnqueueInGame(gameInterface =>
            gameInterface.ReceiveAdvancedCombatState(packet));
}
