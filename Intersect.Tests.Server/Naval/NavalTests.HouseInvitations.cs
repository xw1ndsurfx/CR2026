using Intersect.Server.Housing;
using Intersect.Network.Packets.Housing;
using MessagePack;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test] public void HouseInvitationsArePrivateSingleUseExpiringAndReplacementInvalidatesOldTokens()
    {
        var owner=Guid.NewGuid();var guest=Guid.NewGuid();
        var token=HousingRuntime.CreateHouseInvitation(owner,guest,1000);
        Assert.That(HousingRuntime.TakeHouseInvitation(token,Guid.NewGuid(),1001,out _),Is.False);
        Assert.That(HousingRuntime.TakeHouseInvitation(token,guest,1001,out var actual),Is.True);Assert.That(actual,Is.EqualTo(owner));
        Assert.That(HousingRuntime.TakeHouseInvitation(token,guest,1002,out _),Is.False);
        token=HousingRuntime.CreateHouseInvitation(owner,guest,2000);
        Assert.That(HousingRuntime.TakeHouseInvitation(token,guest,122000,out _),Is.False);
        token=HousingRuntime.CreateHouseInvitation(owner,guest,3000);var replacement=HousingRuntime.CreateHouseInvitation(owner,guest,3001);
        Assert.That(HousingRuntime.TakeHouseInvitation(token,guest,3002,out _),Is.False);
        Assert.That(HousingRuntime.TakeHouseInvitation(replacement,guest,3002,out _),Is.True);
        var packet=new HouseInvitationPacket{Id=token,OwnerId=owner,OwnerName="Friend",ExpiresUtc=123000};
        var copy=MessagePackSerializer.Deserialize<HouseInvitationPacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(copy.OwnerId,Is.EqualTo(owner));Assert.That(copy.Id,Is.EqualTo(token));Assert.That(copy.ExpiresUtc,Is.EqualTo(123000));
    }
}
