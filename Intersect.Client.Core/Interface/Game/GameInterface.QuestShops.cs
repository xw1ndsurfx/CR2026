using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private QuestShopWindow? _questShopWindow;

    public void ApplyQuestShopState(QuestShopStatePacket packet)
    {
        GameMenu?.HideWindows();
        _questShopWindow ??= new QuestShopWindow(GameCanvas);
        _questShopWindow.Apply(packet);
        _questShopWindow.Show();
        _questShopWindow.BringToFront();
    }
}
