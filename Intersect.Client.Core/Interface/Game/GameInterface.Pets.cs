using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private PetsWindow? _petsWindow;

    public void ApplyPetState(PetStatePacket packet)
    {
        _petsWindow?.Apply(packet);
        if (!packet.OpenWindow) return;
        GameMenu?.HideWindows();
        _petsWindow ??= new PetsWindow(GameCanvas);
        _petsWindow.Apply(packet);
        _petsWindow.Show();
        _petsWindow.BringToFront();
    }
}
