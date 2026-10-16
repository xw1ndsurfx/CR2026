using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Input;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Framework.Core.Housing;
using Intersect.Network.Packets.Housing;
using Newtonsoft.Json;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private HousingWindow? _housingWindow;
    private HousePlacementOverlay? _housePlacement;
    private Button? _houseButton, _houseExit;
    public void ApplyHouse(HouseStatePacket packet)
    {
        HouseView? view = string.IsNullOrWhiteSpace(packet.Json) ? null : JsonConvert.DeserializeObject<HouseView>(packet.Json);
        HousingWorld.View = string.IsNullOrWhiteSpace(packet.WorldJson) ? null : JsonConvert.DeserializeObject<HouseView>(packet.WorldJson);
        EnsureHousing();
        _housingWindow!.Apply(view, packet.Message);
        if (packet.OpenWindow) { _housingWindow.Show(); _housingWindow.BringToFront(); }
        if (HousingWorld.View == null) { _housePlacement!.IsHidden = true; if (!packet.OpenWindow) _housingWindow.Hide(); }
    }
    private void EnsureHousing()
    {
        if (_housingWindow != null) return;
        _housingWindow = new HousingWindow(GameCanvas);
        _housePlacement = new HousePlacementOverlay(GameCanvas);
        _housingWindow.PlaceRequested += (id, placement) => _housePlacement.Begin(id, placement);
        _houseButton = new Button(GameCanvas, "MyHouse") { Text = ClientLanguages.Text("My house"), Font = GameContentManager.Current.GetFont("sourcesanspro") ?? GameCanvas.Skin.DefaultFont, FontSize = 9 };
        _houseButton.SetBounds(8, 28, 110, 24); _houseButton.Clicked += (_, _) => PacketSender.SendHouseAction(HouseAction.Open, Globals.Me?.Id ?? Guid.Empty);
        _houseExit = new Button(GameCanvas, "ExitHouse") { Text = ClientLanguages.Text("Leave house"), Font = GameContentManager.Current.GetFont("sourcesanspro") ?? GameCanvas.Skin.DefaultFont, FontSize = 9 };
        _houseExit.SetBounds(124, 28, 120, 24); _houseExit.Clicked += (_, _) => PacketSender.SendHouseAction(HouseAction.Leave);
    }
    private void UpdateHousing()
    {
        EnsureHousing();
        _houseExit!.IsHidden = HousingWorld.View is not { Inside: true };
        _housePlacement!.SetSize(GameCanvas.Width, GameCanvas.Height);
    }
    private sealed class HousePlacementOverlay : Base
    {
        private Guid _furniture, _placement;
        private int _rotation;
        private readonly Label _help;
        public HousePlacementOverlay(Base parent) : base(parent, "HousePlacement")
        {
            IsHidden = true;
            _help = new Label(this) { Text = ClientLanguages.Text("Click to place. Mouse wheel: rotate. Right click: finish."), Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 12, TextColorOverride = Color.White, MouseInputEnabled = false };
            _help.SetBounds(280, 20, 760, 30);
        }
        public void Begin(Guid furniture, Guid placement) { _furniture = furniture; _placement = placement; _rotation = 0; _help.Text = ClientLanguages.Format("Rotation: {0} - click to place, wheel to rotate, right click to finish.", 0); IsHidden = false; BringToFront(); }
        protected override void OnMouseClicked(MouseButton button, Point mousePosition, bool userAction = true)
        {
            if (button == MouseButton.Right) { IsHidden = true; return; }
            if (button != MouseButton.Left || HousingWorld.View is not { Inside: true, IsOwner: true } view || Globals.Me?.MapInstance is not { } map) return;
            var world = Graphics.ConvertToWorldPoint(Globals.InputManager.MousePosition);
            var x = (int)Math.Floor((world.X - map.X) / Options.Instance.Map.TileWidth);
            var y = (int)Math.Floor((world.Y - map.Y) / Options.Instance.Map.TileHeight);
            PacketSender.SendHouseAction(HouseAction.Place, view.OwnerId, _furniture, _placement, x, y, _rotation);
            IsHidden = true;
        }
        protected override bool OnMouseWheeled(int delta) { _rotation = (_rotation + (delta > 0 ? 1 : 3)) % 4; _help.Text = ClientLanguages.Format("Rotation: {0} - click to place, wheel to rotate, right click to finish.", _rotation * 90); return true; }
    }
}
