namespace Intersect.Client.Interface.Game;

public partial class GameInterface
{
    private QuestGuidanceManager? _questGuidance;

    private void UpdateQuestGuidance()
    {
        var guidance = _questGuidance ??= new QuestGuidanceManager(GameCanvas);
        guidance.SuppressOverlay =
            (_worldMapWindow is { IsHidden: false }) ||
            _pokerWindow != null ||
            _blackjackWindow != null ||
            _potionWindow != null;
        guidance.Update();
    }

    private void SetQuestGuidanceOverlaySuppressed(bool suppressed)
    {
        var guidance = _questGuidance ??= new QuestGuidanceManager(GameCanvas);
        guidance.SuppressOverlay = suppressed;
    }

    private void DisposeQuestGuidance()
    {
        _questGuidance?.Clear();
        _questGuidance = null;
    }
}
