using Intersect.Network.Packets.Tutorials;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private TutorialWindow? _tutorialWindow;
    private bool _tutorialEnabled;
    internal bool CanOpenTutorial => _tutorialEnabled && _tutorialWindow != null;
    internal void OpenTutorialGuide() { if (CanOpenTutorial) _tutorialWindow!.OpenGuide(); }
    internal void ApplyTutorial(TutorialStatePacket state)
    {
        _tutorialWindow ??= new TutorialWindow(GameCanvas);
        _tutorialWindow.Apply(state);
        _tutorialEnabled = Newtonsoft.Json.JsonConvert.DeserializeObject<Intersect.Framework.Core.Tutorials.TutorialConfiguration>(state.ConfigurationJson)?.Enabled == true;
    }
    private void UpdateTutorialGuide() => _tutorialWindow?.RefreshVisibility();
}
