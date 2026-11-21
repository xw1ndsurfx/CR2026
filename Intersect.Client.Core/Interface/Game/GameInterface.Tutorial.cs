using Intersect.Network.Packets.Tutorials;
using Intersect.Framework.Core.Tutorials;
using Newtonsoft.Json;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private TutorialWindow? _tutorialWindow;
    private bool _tutorialEnabled;
    private TutorialObjective? _combatTrainingHint;
    internal bool CanOpenTutorial => _tutorialEnabled && _tutorialWindow != null;
    internal void OpenTutorialGuide() { if (CanOpenTutorial) _tutorialWindow!.OpenGuide(); }
    internal void ApplyTutorial(TutorialStatePacket state)
    {
        _tutorialWindow ??= new TutorialWindow(GameCanvas);
        _tutorialWindow.Apply(state);
        var config = JsonConvert.DeserializeObject<TutorialConfiguration>(state.ConfigurationJson);
        _tutorialEnabled = config?.Enabled == true;
        var step = config?.Steps.FirstOrDefault(item => item.Id == state.StepId);
        // The optional hint lives in the existing endurance HUD. It does
        // not consume keyboard focus or create a competing tutorial window.
        _combatTrainingHint = state.Open && _tutorialEnabled && step != null &&
            AdvancedCombatTutorialLessons.IsCombatObjective(step.Objective)
                ? step.Objective : null;
        _advancedCombatHud?.SetTrainingHint(_combatTrainingHint);
    }
    internal void NotifyTutorialHudAction(TutorialHighlightTarget target) =>
        _tutorialWindow?.CompleteOnHudAction(target);
    private void UpdateTutorialGuide() => _tutorialWindow?.RefreshVisibility();
}
