using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Naval;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private NavalBoardingOfferWindow? _boardingOffer;
    public void OfferNavalBoarding(NavalBoardingOfferPacket offer)
    {
        if(Naval.NavalWorld.OwnShip!=null || Globals.Me?.Party.Any(member=>member.Id==offer.CaptainId)!=true)return;
        _boardingOffer??=new NavalBoardingOfferWindow(GameCanvas);_boardingOffer.Apply(offer);
    }
    private sealed class NavalBoardingOfferWindow:Window
    {
        private readonly Label _caption;private long _until;private NavalBoardingOfferPacket _offer=new();
        internal NavalBoardingOfferWindow(Canvas parent):base(parent,QualityOfLifeText.Get("Boarding invitation",ClientLanguages.Current),false,"PartyBoardingOffer")
        {
            SetSize(Math.Min(420,parent.Width-24),150);DeleteOnClose=false;DisableResizing();
            _caption=NavalManagementWindow.Text(this,"BoardingCaption","",16,32,Width-32,46,11);
            NavalManagementWindow.Button(this,"BoardPartyShip",QualityOfLifeText.Get("Board ship",ClientLanguages.Current),16,88,(Width-44)/2,32,()=>
            {
                Networking.Network.SendPacket(new HarbourRequestPacket {View=HarbourView.Crew,Action=HarbourAction.JoinCrew,ShipId=_offer.ShipId});Hide();
            });
            NavalManagementWindow.Button(this,"DeclineBoarding","Close",28+(Width-44)/2,88,(Width-44)/2,32,Hide);Hide();
        }
        protected override void EnsureInitialized() { }
        internal void Apply(NavalBoardingOfferPacket offer)
        {
            _offer=offer;_until=Environment.TickCount64+Math.Clamp(offer.LifetimeSeconds,1,30)*1000;
            _caption.Text=string.Format(QualityOfLifeText.Get("{0} launched {1}. Board with your party?",ClientLanguages.Current),offer.CaptainName,offer.ShipName);
            Tick();Show();BringToFront();
        }
        internal void Tick()
        {
            X=Math.Max(8,((Parent?.Width??Width)-Width)/2);Y=Math.Max(60,(Parent?.Height??Height)-Height-170);
            if(Environment.TickCount64>=_until || Naval.NavalWorld.OwnShip!=null || Globals.Me?.Party.Any(member=>member.Id==_offer.CaptainId)!=true)Hide();
        }
    }
}
