using Intersect.Network;
using Intersect.Network.Packets.Housing;
namespace Intersect.Client.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender sender, HouseStatePacket packet)
    { if (Intersect.Client.Interface.Interface.HasInGameUI) Intersect.Client.Interface.Interface.GameUi.ApplyHouse(packet); }
    public void HandlePacket(IPacketSender sender,HouseInvitationPacket packet)
    {
        if(packet.Id==Guid.Empty || packet.ExpiresUtc<=Intersect.Client.Maps.MapWeatherClock.UtcMilliseconds)return;
        string T(string source)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(source,Intersect.Client.Localization.ClientLanguages.Current);
        _=new Intersect.Client.Interface.Shared.InputBox(T("House invitation"),string.Format(T("{0} invites you to visit their house. Join?"),packet.OwnerName),Intersect.Client.Interface.Shared.InputType.YesNo,
            onSubmit:(_,_)=>PacketSender.SendHouseAction(HouseAction.AcceptInvitation,id:packet.Id),
            onCancel:(_,_)=>PacketSender.SendHouseAction(HouseAction.DeclineInvitation,id:packet.Id));
    }

}
