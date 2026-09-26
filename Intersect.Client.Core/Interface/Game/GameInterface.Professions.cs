using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private ProfessionProgressHud? _professionProgressHud;

    public void ShowProfessionProgress(ProfessionProgressPacket packet)
    {
        _professionProgressHud ??= new ProfessionProgressHud(GameCanvas);
        _professionProgressHud.ShowProgress(packet);
    }

    private void UpdateProfessionProgress()
    {
        _professionProgressHud?.Update();
    }
}
