using Intersect.Network;
using Intersect.Network.Packets.PartyLobbies;
namespace Intersect.Editor.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender,LobbyConfigurationPacket packet)
    {
        General.Globals.MainForm.BeginInvoke((Action)(()=>
        {
            if(packet.Message.Length>0){MessageBox.Show(General.Globals.MainForm,Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(packet.Message,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));return;}
            Application.OpenForms.OfType<Forms.Editors.FrmLobbyConfiguration>().FirstOrDefault()?.Close();
            new Forms.Editors.FrmLobbyConfiguration(packet.Json).Show(General.Globals.MainForm);
        }));
    }
}
