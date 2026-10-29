using Intersect.Client.Framework.Gwen.Control;
using Intersect.Network.Packets.Tutorials;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private TutorialWindow? _tutorialWindow;
    private Button? _tutorialButton;
    internal void ApplyTutorial(TutorialStatePacket state)
    {
        _tutorialWindow ??= new TutorialWindow(GameCanvas);
        _tutorialWindow.Apply(state);
        if (_tutorialButton == null)
        {
            _tutorialButton = NavalManagementWindow.Button(GameCanvas, "TutorialGuideButton", "", 0, 140, 120, 28, () => _tutorialWindow.OpenGuide());
            _tutorialButton.Text = TutorialWindow.T("Tutorial");
        }
        _tutorialButton.IsHidden = !Newtonsoft.Json.JsonConvert.DeserializeObject<Intersect.Framework.Core.Tutorials.TutorialConfiguration>(state.ConfigurationJson)!.Enabled;
    }
    private void UpdateTutorialGuide()
    {
        _tutorialButton?.SetPosition(Math.Max(4, GameCanvas.Width - 132), 140);
        _tutorialWindow?.RefreshVisibility();
    }
}
