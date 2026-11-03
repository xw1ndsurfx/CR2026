using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Enums;
using Intersect.Framework.Core.Tutorials;
namespace Intersect.Client.Interface.Shared;
public partial class SettingsWindow
{
    private Button? _tutorialGuideSetting;
    private void InitializeTutorialSetting()
    {
        _tutorialGuideSetting = new Button(_interfaceSettings, "OpenTutorialGuide")
        {
            Dock = Pos.Top, Height = 32, Margin = new Margin(0, 6, 0, 6), Font = _defaultFont, FontSize = 12,
            Text = TutorialText.Get("Tutorial", ClientLanguages.Current), IsHidden = true,
        };
        _tutorialGuideSetting.Clicked += (_, _) =>
        {
            if (Globals.GameState != GameStates.InGame || Interface.InGameUiOrNull?.CanOpenTutorial != true) return;
            _returnTo = null; Hide();
            Interface.GameUi.OpenTutorialGuide();
        };
    }
    private void UpdateTutorialSetting()
    {
        if (_tutorialGuideSetting == null) return;
        _tutorialGuideSetting.Text = TutorialText.Get("Tutorial", ClientLanguages.Current);
        _tutorialGuideSetting.IsHidden = Globals.GameState != GameStates.InGame || Interface.InGameUiOrNull?.CanOpenTutorial != true;
    }
}
