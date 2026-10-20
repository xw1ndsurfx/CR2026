using DarkUI.Forms;
using Intersect.Editor.Core;
using Intersect.Editor.General;
using Intersect.Editor.Localization;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, NavalConfigurationPacket packet)
    {
        if (Globals.MainForm is not { IsDisposed: false } main) return;
        main.BeginInvoke((Action)(() => main.ReceiveNavalConfiguration(packet)));
    }
    public void HandlePacket(IPacketSender sender, NavalSetupResultPacket packet)
    {
        if (Globals.MainForm is not { IsDisposed: false } main) return;
        main.BeginInvoke((Action)(() =>
        {
            var message = packet.Outcome switch
            {
                NavalSetupOutcome.Created => Strings.Naval.Created.ToString(packet.MapName, packet.X, packet.Y, packet.DockX, packet.DockY),
                NavalSetupOutcome.Existing => Strings.Naval.Existing.ToString(packet.MapName, packet.DockX, packet.DockY),
                NavalSetupOutcome.NotOcean => Strings.Naval.NotOcean.ToString(),
                NavalSetupOutcome.NoSpace => Strings.Naval.NoSpace.ToString(),
                NavalSetupOutcome.ShipLimit => Strings.Naval.ShipLimit.ToString(),
                _ => Strings.Naval.ConfigurationError.ToString(),
            };
            DarkMessageBox.ShowInformation(message, Strings.Naval.Title, DarkDialogButton.Ok, Program.Icon);
        }));
    }
}
